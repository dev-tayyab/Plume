using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Plume.Abstractions;
using Plume.Anthropic.Internal;

namespace Plume.Anthropic;

/// <summary>
/// Anthropic Claude provider. Speaks the Messages API and supports SSE streaming.
/// </summary>
public sealed class AnthropicProvider : IStreamingProvider
{
    /// <summary>The default Anthropic API version header.</summary>
    public const string DefaultApiVersion = "2023-06-01";

    private readonly HttpClient _http;
    private readonly AnthropicProviderOptions _options;

    /// <summary>Create a new Anthropic provider.</summary>
    public AnthropicProvider(HttpClient http, AnthropicProviderOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new ArgumentException("ApiKey is required.", nameof(options));

        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        _http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        _http.DefaultRequestHeaders.TryAddWithoutValidation(
            "anthropic-version", _options.ApiVersion);
    }

    /// <inheritdoc />
    public string Name => "anthropic";

    /// <inheritdoc />
    public bool Supports(string model) =>
        !string.IsNullOrEmpty(model)
        && model.StartsWith("claude-", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<ProviderResponse> SendAsync(
        ProviderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = MapToAnthropic(request, streaming: false);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(
                payload, AnthropicJsonContext.Default.AnthropicMessagesRequest)
        };

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        var raw = await response.Content
            .ReadFromJsonAsync(AnthropicJsonContext.Default.AnthropicMessageResponse, ct)
            .ConfigureAwait(false);

        if (raw is null)
            throw new ProviderRequestException(Name, "Empty response from Anthropic.");

        // Concatenate all text content blocks
        var contentText = raw.Content is null
            ? string.Empty
            : string.Concat(raw.Content
                .Where(c => c.Type == "text" && c.Text is not null)
                .Select(c => c.Text!));

        return new ProviderResponse
        {
            Content = contentText,
            Model = raw.Model ?? request.Model,
            FinishReason = MapFinishReason(raw.StopReason),
            Usage = raw.Usage is { } u
                ? new TokenUsage(u.InputTokens, u.OutputTokens)
                : null,
            Metadata = raw.Id is null ? null : new Dictionary<string, object?>
            {
                ["id"] = raw.Id
            }
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ProviderStreamChunk> StreamAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = MapToAnthropic(request, streaming: true);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(
                payload, AnthropicJsonContext.Default.AnthropicMessagesRequest)
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        int? promptTokens = null;
        int? completionTokens = null;

        await foreach (var sse in SseEventReader.ReadAsync(stream, ct).ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(sse.Data))
                continue;

            // Anthropic sends typed events; we only act on a subset
            AnthropicStreamEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize(
                    sse.Data, AnthropicJsonContext.Default.AnthropicStreamEvent);
            }
            catch (JsonException)
            {
                continue;
            }

            if (evt is null) continue;

            switch (evt.Type)
            {
                case "message_start":
                    promptTokens = evt.Message?.Usage?.InputTokens;
                    break;

                case "content_block_delta":
                    if (evt.Delta?.Type == "text_delta" && evt.Delta.Text is { Length: > 0 } text)
                    {
                        yield return new ProviderStreamChunk { Content = text };
                    }
                    break;

                case "message_delta":
                    completionTokens = evt.Usage?.OutputTokens;
                    if (evt.Delta?.StopReason is { } stop)
                    {
                        yield return new ProviderStreamChunk
                        {
                            Content = string.Empty,
                            IsFinal = true,
                            FinishReason = MapFinishReason(stop),
                            Usage = (promptTokens, completionTokens) is (int p, int c)
                                ? new TokenUsage(p, c)
                                : null
                        };
                    }
                    break;

                case "message_stop":
                    yield break;

                case "error":
                    throw new ProviderTransientException(Name,
                        $"Stream error: {sse.Data}");

                default:
                    // ping, content_block_start, content_block_stop — ignore
                    break;
            }
        }
    }

    private static AnthropicMessagesRequest MapToAnthropic(ProviderRequest request, bool streaming)
    {
        var ext = request.Extensions as AnthropicExtensions;

        // Anthropic uses a top-level `system` field, not a system message in the array.
        string? system = null;
        var msgs = new List<AnthropicMessage>(request.Messages.Count);

        foreach (var m in request.Messages)
        {
            if (m.Role == MessageRole.System)
            {
                system = system is null ? m.Content : (system + "\n\n" + m.Content);
                continue;
            }

            msgs.Add(new AnthropicMessage
            {
                Role = m.Role switch
                {
                    MessageRole.User => "user",
                    MessageRole.Assistant => "assistant",
                    MessageRole.Tool => "user", // Anthropic doesn't have a "tool" role at this layer
                    _ => "user"
                },
                Content = m.Content
            });
        }

        // max_tokens is REQUIRED by Anthropic. Apply a sensible default if absent.
        var maxTokens = request.MaxTokens ?? 1024;

        return new AnthropicMessagesRequest
        {
            Model = request.Model,
            Messages = msgs,
            System = system,
            MaxTokens = maxTokens,
            Temperature = request.Temperature,
            TopK = ext?.TopK,
            TopP = ext?.TopP,
            StopSequences = request.StopSequences?.ToList(),
            Stream = streaming ? true : null,
            Metadata = ext?.UserId is null ? null : new AnthropicMetadata { UserId = ext.UserId }
        };
    }

    private static FinishReason MapFinishReason(string? reason) => reason switch
    {
        "end_turn" => FinishReason.Stop,
        "stop_sequence" => FinishReason.Stop,
        "max_tokens" => FinishReason.Length,
        null => FinishReason.Stop,
        _ => FinishReason.Other
    };
}

/// <summary>Configuration for <see cref="AnthropicProvider"/>.</summary>
public sealed class AnthropicProviderOptions
{
    /// <summary>Anthropic API key.</summary>
    public required string ApiKey { get; init; }

    /// <summary>Base URL.</summary>
    public string BaseUrl { get; init; } = "https://api.anthropic.com";

    /// <summary>Anthropic API version header.</summary>
    public string ApiVersion { get; init; } = AnthropicProvider.DefaultApiVersion;
}
