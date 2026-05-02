using System.Net.Http.Json;
using Plume.Abstractions;
using Plume.Ollama.Internal;

namespace Plume.Ollama;

/// <summary>
/// Ollama embedding provider for local models. Uses the /api/embed endpoint which
/// supports multi-input batches (Ollama 0.1.34+).
/// </summary>
public sealed class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _http;
    private readonly OllamaProviderOptions _options;

    /// <summary>Create a new Ollama embedding provider.</summary>
    public OllamaEmbeddingProvider(HttpClient http, OllamaProviderOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _http.BaseAddress ??= new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    }

    /// <inheritdoc />
    public string Name => "ollama";

    /// <inheritdoc />
    public bool Supports(string model)
    {
        if (string.IsNullOrEmpty(model)) return false;
        return _options.RequiredModelPrefix is null
            || model.StartsWith(_options.RequiredModelPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = new OllamaEmbedRequest
        {
            Model = request.Model,
            Input = request.Inputs.ToList()
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/embed");
        httpRequest.Content = JsonContent.Create(payload, OllamaJsonContext.Default.OllamaEmbedRequest);

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        var raw = await response.Content
            .ReadFromJsonAsync(OllamaJsonContext.Default.OllamaEmbedResponse, ct)
            .ConfigureAwait(false);

        if (raw is null || raw.Embeddings is null || raw.Embeddings.Count == 0)
            throw new ProviderRequestException(Name, "Empty embeddings response from Ollama.");

        var vectors = raw.Embeddings
            .Select(v => new Embedding(v ?? Array.Empty<float>()))
            .ToList();

        return new EmbeddingResponse
        {
            Embeddings = vectors,
            Model = raw.Model ?? request.Model,
            Usage = raw.PromptEvalCount is int p ? new TokenUsage(p, 0) : null
        };
    }
}
