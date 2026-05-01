using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Plume.Abstractions;
using Plume.OpenAI.Internal;

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

        return new ProviderResponse
        {
            Content = content,
            Model = raw.Model ?? request.Model,
            FinishReason = MapFinishReason(choice.FinishReason),
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

        var messages = request.Messages.Select(m => new OpenAiMessage
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
        }).ToList();

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
            ResponseFormat = ext?.ResponseFormat switch
            {
                OpenAIResponseFormat.JsonObject => new OpenAiResponseFormatPayload { Type = "json_object" },
                _ => null
            }
        };
    }

    private static FinishReason MapFinishReason(string? reason) => reason switch
    {
        "stop" => FinishReason.Stop,
        "length" => FinishReason.Length,
        "content_filter" => FinishReason.ContentFilter,
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
