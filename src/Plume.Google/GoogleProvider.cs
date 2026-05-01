using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Plume.Abstractions;
using Plume.Google.Internal;

namespace Plume.Google;

/// <summary>
/// Google Gemini provider. Uses the Generative Language API with SSE streaming.
/// </summary>
public sealed class GoogleProvider : IStreamingProvider
{
    private readonly HttpClient _http;
    private readonly GoogleProviderOptions _options;

    /// <summary>Create a new Google provider.</summary>
    public GoogleProvider(HttpClient http, GoogleProviderOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new ArgumentException("ApiKey is required.", nameof(options));

        _http.BaseAddress ??= new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        _http.DefaultRequestHeaders.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
    }

    /// <inheritdoc />
    public string Name => "google";

    /// <inheritdoc />
    public bool Supports(string model) =>
        !string.IsNullOrEmpty(model)
        && model.StartsWith("gemini", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<ProviderResponse> SendAsync(
        ProviderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = MapToGoogle(request);
        var url = $"{_options.ApiVersion}/models/{Uri.EscapeDataString(request.Model)}:generateContent";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, GoogleJsonContext.Default.GoogleGenerateRequest)
        };

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        var raw = await response.Content
            .ReadFromJsonAsync(GoogleJsonContext.Default.GoogleGenerateResponse, ct)
            .ConfigureAwait(false);

        if (raw is null || raw.Candidates is null || raw.Candidates.Count == 0)
            throw new ProviderRequestException(Name, "Empty response from Google.");

        var candidate = raw.Candidates[0];
        var contentText = candidate.Content?.Parts is null
            ? string.Empty
            : string.Concat(candidate.Content.Parts.Select(p => p.Text));

        return new ProviderResponse
        {
            Content = contentText,
            Model = raw.ModelVersion ?? request.Model,
            FinishReason = MapFinishReason(candidate.FinishReason),
            Usage = raw.UsageMetadata is { } u
                ? new TokenUsage(u.PromptTokenCount, u.CandidatesTokenCount)
                : null
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ProviderStreamChunk> StreamAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = MapToGoogle(request);
        var url = $"{_options.ApiVersion}/models/{Uri.EscapeDataString(request.Model)}:streamGenerateContent?alt=sse";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, GoogleJsonContext.Default.GoogleGenerateRequest)
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        await foreach (var sse in SseEventReader.ReadAsync(stream, ct).ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(sse.Data)) continue;

            GoogleGenerateResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize(
                    sse.Data, GoogleJsonContext.Default.GoogleGenerateResponse);
            }
            catch (JsonException) { continue; }

            if (chunk is null) continue;

            var candidate = chunk.Candidates?.FirstOrDefault();
            var text = candidate?.Content?.Parts is null
                ? string.Empty
                : string.Concat(candidate.Content.Parts.Select(p => p.Text));
            var finish = candidate?.FinishReason;
            var usage = chunk.UsageMetadata;

            if (string.IsNullOrEmpty(text) && finish is null && usage is null) continue;

            yield return new ProviderStreamChunk
            {
                Content = text,
                IsFinal = finish is not null,
                FinishReason = finish is null ? null : MapFinishReason(finish),
                Usage = usage is null ? null : new TokenUsage(usage.PromptTokenCount, usage.CandidatesTokenCount)
            };
        }
    }

    private static GoogleGenerateRequest MapToGoogle(ProviderRequest request)
    {
        var ext = request.Extensions as GoogleExtensions;

        GoogleContent? systemInstruction = null;
        var contents = new List<GoogleContent>(request.Messages.Count);

        foreach (var m in request.Messages)
        {
            if (m.Role == MessageRole.System)
            {
                systemInstruction ??= new GoogleContent { Parts = new List<GooglePart>() };
                systemInstruction.Parts.Add(new GooglePart { Text = m.Content });
                continue;
            }

            contents.Add(new GoogleContent
            {
                Role = m.Role switch
                {
                    MessageRole.User => "user",
                    MessageRole.Assistant => "model",
                    MessageRole.Tool => "user",
                    _ => "user"
                },
                Parts = [new GooglePart { Text = m.Content }]
            });
        }

        return new GoogleGenerateRequest
        {
            Contents = contents,
            SystemInstruction = systemInstruction,
            GenerationConfig = new GoogleGenerationConfig
            {
                Temperature = request.Temperature,
                MaxOutputTokens = request.MaxTokens,
                TopP = ext?.TopP,
                TopK = ext?.TopK,
                CandidateCount = ext?.CandidateCount,
                StopSequences = request.StopSequences?.ToList()
            }
        };
    }

    private static FinishReason MapFinishReason(string? reason) => reason switch
    {
        "STOP" => FinishReason.Stop,
        "MAX_TOKENS" => FinishReason.Length,
        "SAFETY" => FinishReason.ContentFilter,
        "RECITATION" => FinishReason.ContentFilter,
        null => FinishReason.Stop,
        _ => FinishReason.Other
    };
}

/// <summary>Configuration for <see cref="GoogleProvider"/>.</summary>
public sealed class GoogleProviderOptions
{
    /// <summary>Google API key (from Google AI Studio).</summary>
    public required string ApiKey { get; init; }

    /// <summary>Base URL.</summary>
    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com";

    /// <summary>API version segment, e.g. "beta".</summary>
    public string ApiVersion { get; init; } = "v1beta";
}
