using System.Net.Http.Json;
using Plume.Abstractions;
using Plume.Google.Internal;

namespace Plume.Google;

/// <summary>
/// Google Gemini embedding provider. Uses the batchEmbedContents endpoint of the
/// Generative Language API.
/// </summary>
public sealed class GoogleEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _http;
    private readonly GoogleProviderOptions _options;

    /// <summary>Create a new Google embedding provider.</summary>
    public GoogleEmbeddingProvider(HttpClient http, GoogleProviderOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new ArgumentException("ApiKey is required.", nameof(options));

        _http.BaseAddress ??= new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        if (!_http.DefaultRequestHeaders.Contains("x-goog-api-key"))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
    }

    /// <inheritdoc />
    public string Name => "google";

    /// <inheritdoc />
    public bool Supports(string model)
    {
        if (string.IsNullOrEmpty(model)) return false;
        return model.StartsWith("text-embedding-", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("embedding-", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("gemini-embedding-", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var modelPath = request.Model.StartsWith("models/", StringComparison.Ordinal)
            ? request.Model
            : $"models/{request.Model}";

        var requests = request.Inputs.Select(input => new GoogleEmbedSingleRequest
        {
            Model = modelPath,
            Content = new GoogleContent
            {
                Parts = new List<GooglePart> { new() { Text = input } }
            },
            OutputDimensionality = request.Dimensions
        }).ToList();

        var payload = new GoogleBatchEmbedRequest { Requests = requests };

        var url = $"{_options.ApiVersion}/{modelPath}:batchEmbedContents";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, GoogleJsonContext.Default.GoogleBatchEmbedRequest)
        };

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        var raw = await response.Content
            .ReadFromJsonAsync(GoogleJsonContext.Default.GoogleBatchEmbedResponse, ct)
            .ConfigureAwait(false);

        if (raw is null || raw.Embeddings is null || raw.Embeddings.Count == 0)
            throw new ProviderRequestException(Name, "Empty embeddings response from Google.");

        var vectors = raw.Embeddings
            .Select(e => new Embedding(e.Values ?? Array.Empty<float>()))
            .ToList();

        return new EmbeddingResponse
        {
            Embeddings = vectors,
            Model = request.Model
        };
    }
}
