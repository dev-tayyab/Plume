using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Plume.Abstractions;
using Plume.OpenAI.Internal;
using Plume.Tools;

namespace Plume.OpenAI;

/// <summary>
/// OpenAI provider. Speaks the Chat Completions API and supports streaming via SSE.
/// Compatible with Azure OpenAI, OpenRouter, and any OpenAI-compatible endpoint by
/// configuring <see cref="OpenAiProviderOptions.BaseUrl"/>.
/// </summary>
public sealed class OpenAiProvider : IStreamingProvider
{
    private readonly HttpClient _http;
    private readonly OpenAiProviderOptions _options;

    /// <summary>Create a new OpenAI provider.</summary>
    public OpenAiProvider(HttpClient http, OpenAiProviderOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new ArgumentException("ApiKey is required.", nameof(options));

        _http.BaseAddress ??= new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        if (!string.IsNullOrWhiteSpace(_options.Organization))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("OpenAI-Organization", _options.Organization);
    }

    /// <inheritdoc />
    public string Name => "openai";

    /// <inheritdoc />
    public bool Supports(string model)
    {
        if (string.IsNullOrEmpty(model)) return false;

        // OpenAI's modern model families. Includes gpt-*, o1, o3, o4 reasoning models.
        return model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("o3", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("o4", StringComparison.OrdinalIgnoreCase)
            || (_options.AcceptAnyModel);
    }

    /// <inheritdoc />
    public async Task<ProviderResponse> SendAsync(
        ProviderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = MapToOpenAi(request, streaming: false);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        httpRequest.Content = JsonContent.Create(
            payload, OpenAiJsonContext.Default.OpenAiChatRequest);

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        var raw = await response.Content
            .ReadFromJsonAsync(OpenAiJsonContext.Default.OpenAiChatResponse, ct)
            .ConfigureAwait(false);

        if (raw is null || raw.Choices is null || raw.Choices.Count == 0)
            throw new ProviderRequestException(Name, "Empty response from OpenAI.");

        var choice = raw.Choices[0];
        var content = choice.Message?.Content ?? string.Empty;
        var toolCalls = MapToolCallsFromOpenAi(choice.Message?.ToolCalls);

        return new ProviderResponse
        {
            Content = content,
            Model = raw.Model ?? request.Model,
            FinishReason = MapFinishReason(choice.FinishReason),
            ToolCalls = toolCalls,
            Usage = raw.Usage is { } u
                ? new TokenUsage(u.PromptTokens, u.CompletionTokens)
                : null,
            Metadata = raw.Id is null ? null : new Dictionary<string, object?>
            {
                ["id"] = raw.Id,
                ["system_fingerprint"] = raw.SystemFingerprint
            }
        };
    }

    private static List<ToolCall>? MapToolCallsFromOpenAi(List<OpenAiToolCall>? src)
    {
        if (src is null || src.Count == 0) return null;
        var list = new List<ToolCall>(src.Count);
        foreach (var c in src)
        {
            list.Add(new ToolCall
            {
                Id = c.Id,
                Name = c.Function.Name,
                ArgumentsJson = c.Function.Arguments
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

        var payload = MapToOpenAi(request, streaming: true);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        httpRequest.Content = JsonContent.Create(
            payload, OpenAiJsonContext.Default.OpenAiChatRequest);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        await foreach (var sse in SseEventReader.ReadAsync(stream, ct).ConfigureAwait(false))
        {
            // OpenAI uses a sentinel "[DONE]" line, no JSON payload.
            if (sse.Data == "[DONE]")
                yield break;

            if (string.IsNullOrEmpty(sse.Data))
                continue;

            OpenAiChatResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize(
                    sse.Data, OpenAiJsonContext.Default.OpenAiChatResponse);
            }
            catch (JsonException)
            {
                // skip malformed chunk silently — OpenAI occasionally emits these
                continue;
            }

            if (chunk is null) continue;

            var choice = chunk.Choices?.FirstOrDefault();
            var delta = choice?.Delta?.Content ?? string.Empty;
            var finish = choice?.FinishReason;
            var usage = chunk.Usage;

            yield return new ProviderStreamChunk
            {
                Content = delta,
                IsFinal = finish is not null,
                FinishReason = finish is null ? null : MapFinishReason(finish),
                Usage = usage is null ? null : new TokenUsage(usage.PromptTokens, usage.CompletionTokens)
            };
        }
    }

    private static OpenAiChatRequest MapToOpenAi(ProviderRequest request, bool streaming)
    {
        var ext = request.Extensions as OpenAIExtensions;

        var messages = request.Messages.Select(MapMessageToOpenAi).ToList();

        return new OpenAiChatRequest
        {
            Model = request.Model,
            Messages = messages,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Stop = request.StopSequences?.ToList(),
            Stream = streaming ? true : null,
            StreamOptions = streaming ? new OpenAiStreamOptions() : null,
            FrequencyPenalty = ext?.FrequencyPenalty,
            PresencePenalty = ext?.PresencePenalty,
            Seed = ext?.Seed,
            User = ext?.User,
            TopP = ext?.TopP,
            ResponseFormat = MapResponseFormat(request, ext),
            Tools = MapTools(request.Tools),
            ToolChoice = MapToolChoice(request.ToolChoice, request.Tools)
        };
    }

    private static OpenAiMessage MapMessageToOpenAi(Message m)
    {
        var msg = new OpenAiMessage
        {
            Role = m.Role switch
            {
                MessageRole.System => "system",
                MessageRole.User => "user",
                MessageRole.Assistant => "assistant",
                MessageRole.Tool => "tool",
                _ => "user"
            },
            Content = string.IsNullOrEmpty(m.Content) ? null : m.Content,
            ToolCallId = m.ToolCallId
        };

        if (m.ToolCalls is { Count: > 0 } calls)
        {
            msg.ToolCalls = calls.Select(c => new OpenAiToolCall
            {
                Id = c.Id,
                Type = "function",
                Function = new OpenAiFunctionCall { Name = c.Name, Arguments = c.ArgumentsJson }
            }).ToList();
        }

        return msg;
    }

    private static List<OpenAiTool>? MapTools(IReadOnlyList<Tool>? tools)
    {
        if (tools is null || tools.Count == 0) return null;
        var list = new List<OpenAiTool>(tools.Count);
        foreach (var t in tools)
        {
            using var doc = JsonDocument.Parse(t.ParametersJsonSchema);
            list.Add(new OpenAiTool
            {
                Type = "function",
                Function = new OpenAiFunctionDef
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = doc.RootElement.Clone()
                }
            });
        }
        return list;
    }

    private static JsonElement? MapToolChoice(ToolChoice? choice, IReadOnlyList<Tool>? tools)
    {
        if (tools is null || tools.Count == 0) return null;
        return choice switch
        {
            null or ToolChoice.Auto => StringElement("auto"),
            ToolChoice.None => StringElement("none"),
            ToolChoice.Required => StringElement("required"),
            ToolChoice.Specific s => SpecificToolChoiceElement(s.Name),
            _ => StringElement("auto")
        };

        static JsonElement StringElement(string value)
        {
            // JsonDocument.Parse handles quoting and escaping safely without
            // requiring runtime reflection-based serialization.
            using var buf = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(buf)) writer.WriteStringValue(value);
            buf.Position = 0;
            using var doc = JsonDocument.Parse(buf);
            return doc.RootElement.Clone();
        }

        static JsonElement SpecificToolChoiceElement(string toolName)
        {
            using var buf = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(buf))
            {
                writer.WriteStartObject();
                writer.WriteString("type", "function");
                writer.WriteStartObject("function");
                writer.WriteString("name", toolName);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            buf.Position = 0;
            using var doc = JsonDocument.Parse(buf);
            return doc.RootElement.Clone();
        }
    }

    private static OpenAiResponseFormatPayload? MapResponseFormat(
        ProviderRequest request, OpenAIExtensions? ext)
    {
        // Provider-agnostic structured output takes precedence.
        if (request.ResponseSchema is { } spec)
        {
            if (string.IsNullOrEmpty(spec.SchemaJson))
                return new OpenAiResponseFormatPayload { Type = "json_object" };

            using var doc = JsonDocument.Parse(spec.SchemaJson);
            return new OpenAiResponseFormatPayload
            {
                Type = "json_schema",
                JsonSchema = new OpenAiJsonSchemaSpec
                {
                    Name = spec.Name ?? "response",
                    Strict = spec.Strict,
                    Schema = doc.RootElement.Clone()
                }
            };
        }

        return ext?.ResponseFormat switch
        {
            OpenAIResponseFormat.JsonObject => new OpenAiResponseFormatPayload { Type = "json_object" },
            _ => null
        };
    }

    private static FinishReason MapFinishReason(string? reason) => reason switch
    {
        "stop" => FinishReason.Stop,
        "length" => FinishReason.Length,
        "content_filter" => FinishReason.ContentFilter,
        "tool_calls" => FinishReason.ToolCalls,
        null => FinishReason.Stop,
        _ => FinishReason.Other
    };
}

/// <summary>Configuration for <see cref="OpenAiProvider"/>.</summary>
public sealed class OpenAiProviderOptions
{
    /// <summary>OpenAI API key.</summary>
    public required string ApiKey { get; init; }

    /// <summary>Base URL. Override for Azure OpenAI, OpenRouter, or any compatible endpoint.</summary>
    public string BaseUrl { get; init; } = "https://api.openai.com";

    /// <summary>Optional OpenAI-Organization header.</summary>
    public string? Organization { get; init; }

    /// <summary>
    /// If true, accept any model name (useful for OpenAI-compatible proxies).
    /// Default: only known OpenAI model prefixes.
    /// </summary>
    public bool AcceptAnyModel { get; init; }
}
