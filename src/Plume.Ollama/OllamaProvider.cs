using System.IO;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Plume.Abstractions;
using Plume.Ollama.Internal;
using Plume.Tools;

namespace Plume.Ollama;

/// <summary>
/// Ollama provider for local models. Uses /api/chat with NDJSON streaming.
/// </summary>
public sealed class OllamaProvider : IStreamingProvider
{
    private static readonly JsonElement JsonModeFormat =
        JsonDocument.Parse("\"json\"").RootElement.Clone();

    private readonly HttpClient _http;
    private readonly OllamaProviderOptions _options;

    /// <summary>Create a new Ollama provider.</summary>
    public OllamaProvider(HttpClient http, OllamaProviderOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _http.BaseAddress ??= new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    }

    /// <inheritdoc />
    public string Name => "ollama";

    /// <summary>
    /// Ollama hosts arbitrary local models, so by default Plume routes any model
    /// to it as a last-resort fallback when no other provider supports the model.
    /// Set <see cref="OllamaProviderOptions.RequiredModelPrefix"/> to filter explicitly.
    /// </summary>
    public bool Supports(string model)
    {
        if (string.IsNullOrEmpty(model)) return false;
        return _options.RequiredModelPrefix is null
            || model.StartsWith(_options.RequiredModelPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<ProviderResponse> SendAsync(
        ProviderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = MapToOllama(request, streaming: false);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat");
        httpRequest.Content = JsonContent.Create(payload, OllamaJsonContext.Default.OllamaChatRequest);

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        var raw = await response.Content
            .ReadFromJsonAsync(OllamaJsonContext.Default.OllamaChatResponse, ct)
            .ConfigureAwait(false);

        if (raw is null)
            throw new ProviderRequestException(Name, "Empty response from Ollama.");

        var toolCalls = MapToolCallsFromOllama(raw.Message?.ToolCalls);

        return new ProviderResponse
        {
            Content = raw.Message?.Content ?? string.Empty,
            Model = raw.Model ?? request.Model,
            FinishReason = toolCalls is { Count: > 0 } ? FinishReason.ToolCalls : MapFinishReason(raw.DoneReason),
            ToolCalls = toolCalls,
            Usage = (raw.PromptEvalCount, raw.EvalCount) is (int p, int c)
                ? new TokenUsage(p, c)
                : null
        };
    }

    private static List<ToolCall>? MapToolCallsFromOllama(List<OllamaToolCall>? src)
    {
        if (src is null || src.Count == 0) return null;
        var list = new List<ToolCall>(src.Count);
        for (int i = 0; i < src.Count; i++)
        {
            var c = src[i];
            list.Add(new ToolCall
            {
                // Ollama doesn't supply call IDs — synthesize one.
                Id = $"{c.Function.Name}-{i}",
                Name = c.Function.Name,
                ArgumentsJson = c.Function.Arguments.GetRawText()
            });
        }
        return list;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ProviderStreamChunk> StreamAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = MapToOllama(request, streaming: true);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = JsonContent.Create(payload, OllamaJsonContext.Default.OllamaChatRequest)
        };

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaChatResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize(line, OllamaJsonContext.Default.OllamaChatResponse);
            }
            catch (JsonException) { continue; }

            if (chunk is null) continue;

            var content = chunk.Message?.Content ?? string.Empty;

            yield return new ProviderStreamChunk
            {
                Content = content,
                IsFinal = chunk.Done,
                FinishReason = chunk.Done ? MapFinishReason(chunk.DoneReason) : null,
                Usage = chunk.Done && (chunk.PromptEvalCount, chunk.EvalCount) is (int p, int c)
                    ? new TokenUsage(p, c)
                    : null
            };

            if (chunk.Done) yield break;
        }
    }

    private static OllamaChatRequest MapToOllama(ProviderRequest request, bool streaming)
    {
        var ext = request.Extensions as OllamaExtensions;

        var messages = request.Messages.Select(MapMessageToOllama).ToList();

        var options = new OllamaModelOptions
        {
            Temperature = request.Temperature,
            NumPredict = request.MaxTokens,
            TopK = ext?.TopK,
            TopP = ext?.TopP,
            Seed = ext?.Seed,
            Stop = request.StopSequences?.ToList()
        };

        // Only attach options if at least one is set
        var hasOptions = options.Temperature.HasValue
            || options.NumPredict.HasValue
            || options.TopK.HasValue
            || options.TopP.HasValue
            || options.Seed.HasValue
            || (options.Stop is { Count: > 0 });

        JsonElement? format = null;
        if (request.ResponseSchema is { } spec)
        {
            if (string.IsNullOrEmpty(spec.SchemaJson))
            {
                format = JsonModeFormat;
            }
            else
            {
                using var doc = JsonDocument.Parse(spec.SchemaJson);
                format = doc.RootElement.Clone();
            }
        }

        return new OllamaChatRequest
        {
            Model = request.Model,
            Messages = messages,
            Stream = streaming,
            KeepAlive = ext?.KeepAlive,
            Options = hasOptions ? options : null,
            Format = format,
            Tools = MapOllamaTools(request.Tools)
        };
    }

    private static OllamaMessage MapMessageToOllama(Message m)
    {
        var msg = new OllamaMessage
        {
            Role = m.Role switch
            {
                MessageRole.System => "system",
                MessageRole.User => "user",
                MessageRole.Assistant => "assistant",
                MessageRole.Tool => "tool",
                _ => "user"
            },
            Content = m.Content
        };

        if (m.ToolCalls is { Count: > 0 } calls)
        {
            msg.ToolCalls = calls.Select(c =>
            {
                using var doc = JsonDocument.Parse(string.IsNullOrEmpty(c.ArgumentsJson) ? "{}" : c.ArgumentsJson);
                return new OllamaToolCall
                {
                    Function = new OllamaFunctionInvocation { Name = c.Name, Arguments = doc.RootElement.Clone() }
                };
            }).ToList();
        }

        return msg;
    }

    private static List<OllamaTool>? MapOllamaTools(IReadOnlyList<Tool>? tools)
    {
        if (tools is null || tools.Count == 0) return null;
        var list = new List<OllamaTool>(tools.Count);
        foreach (var t in tools)
        {
            using var doc = JsonDocument.Parse(t.ParametersJsonSchema);
            list.Add(new OllamaTool
            {
                Type = "function",
                Function = new OllamaFunctionDef
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = doc.RootElement.Clone()
                }
            });
        }
        return list;
    }

    private static FinishReason MapFinishReason(string? reason) => reason switch
    {
        "stop" => FinishReason.Stop,
        "length" => FinishReason.Length,
        null => FinishReason.Stop,
        _ => FinishReason.Other
    };
}

/// <summary>Configuration for <see cref="OllamaProvider"/>.</summary>
public sealed class OllamaProviderOptions
{
    /// <summary>Base URL of the Ollama server.</summary>
    public string BaseUrl { get; init; } = "http://localhost:11434";

    /// <summary>
    /// If set, only models with this prefix will be claimed as supported.
    /// Useful for filtering during failover. Default: accept any model.
    /// </summary>
    public string? RequiredModelPrefix { get; init; }
}
