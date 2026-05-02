using System.Net.Http.Headers;
using System.Net.Http.Json;
using Plume.Abstractions;
using Plume.OpenAI.Internal;

namespace Plume.OpenAI;

/// <summary>
/// OpenAI embedding provider. Speaks the /v1/embeddings API and is compatible with
/// Azure OpenAI and any OpenAI-compatible endpoint via <see cref="OpenAiProviderOptions.BaseUrl"/>.
/// </summary>
public sealed class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _http;
    private readonly OpenAiProviderOptions _options;

    /// <summary>Create a new OpenAI embedding provider.</summary>
    public OpenAiEmbeddingProvider(HttpClient http, OpenAiProviderOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new ArgumentException("ApiKey is required.", nameof(options));

        _http.BaseAddress ??= new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        _http.DefaultRequestHeaders.Authorization ??=
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        if (!string.IsNullOrWhiteSpace(_options.Organization)
            && !_http.DefaultRequestHeaders.Contains("OpenAI-Organization"))
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("OpenAI-Organization", _options.Organization);
        }
    }

    /// <inheritdoc />
    public string Name => "openai";

    /// <inheritdoc />
    public bool Supports(string model)
    {
        if (string.IsNullOrEmpty(model)) return false;
        return model.StartsWith("text-embedding-", StringComparison.OrdinalIgnoreCase)
            || _options.AcceptAnyModel;
    }

    /// <inheritdoc />
    public async Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = new OpenAiEmbeddingRequest
        {
            Model = request.Model,
            Input = request.Inputs.ToList(),
            EncodingFormat = "float",
            Dimensions = request.Dimensions
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/embeddings");
        httpRequest.Content = JsonContent.Create(
            payload, OpenAiEmbeddingJsonContext.Default.OpenAiEmbeddingRequest);

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        var raw = await response.Content
            .ReadFromJsonAsync(OpenAiEmbeddingJsonContext.Default.OpenAiEmbeddingResponse, ct)
            .ConfigureAwait(false);

        if (raw is null || raw.Data is null || raw.Data.Count == 0)
            throw new ProviderRequestException(Name, "Empty embeddings response from OpenAI.");

        // Preserve order using the index field; OpenAI returns sorted but the API allows any order.
        var ordered = raw.Data
            .OrderBy(d => d.Index)
            .Select(d => new Embedding(d.Embedding ?? Array.Empty<float>()))
            .ToList();

        return new EmbeddingResponse
        {
            Embeddings = ordered,
            Model = raw.Model ?? request.Model,
            Usage = raw.Usage is { } u
                ? new TokenUsage(u.PromptTokens, 0)
                : null
        };
    }
}
