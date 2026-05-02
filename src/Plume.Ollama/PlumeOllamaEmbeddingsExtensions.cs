using Microsoft.Extensions.DependencyInjection;

namespace Plume.Ollama;

/// <summary>Extensions to register Ollama as a Plume embedding provider.</summary>
public static class PlumeOllamaEmbeddingsExtensions
{
    extension(PlumeEmbeddingOptions options)
    {
        /// <summary>Configure Ollama as the primary embedding provider.</summary>
        public PlumeEmbeddingOptions UseOllama(string? baseUrl = null, string? requiredModelPrefix = null)
            => options.Use(sp => CreateProvider(sp, baseUrl, requiredModelPrefix));

        /// <summary>Configure Ollama as a fallback embedding provider.</summary>
        public PlumeEmbeddingOptions FallbackToOllama(string? baseUrl = null, string? requiredModelPrefix = null)
            => options.AddFallback(sp => CreateProvider(sp, baseUrl, requiredModelPrefix));
    }

    private static OllamaEmbeddingProvider CreateProvider(
        IServiceProvider sp,
        string? baseUrl,
        string? requiredModelPrefix)
    {
        var httpFactory = sp.GetService<IHttpClientFactory>();
        var http = httpFactory?.CreateClient("Plume.Ollama.Embeddings") ?? new HttpClient();

        if (http.Timeout == TimeSpan.FromSeconds(100)) // default
            http.Timeout = TimeSpan.FromMinutes(5);

        var providerOptions = new OllamaProviderOptions
        {
            BaseUrl = baseUrl ?? "http://localhost:11434",
            RequiredModelPrefix = requiredModelPrefix
        };

        return new OllamaEmbeddingProvider(http, providerOptions);
    }
}
