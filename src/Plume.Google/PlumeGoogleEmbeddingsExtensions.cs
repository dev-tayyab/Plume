using Microsoft.Extensions.DependencyInjection;

namespace Plume.Google;

/// <summary>Extensions to register Google Gemini as a Plume embedding provider.</summary>
public static class PlumeGoogleEmbeddingsExtensions
{
    extension(PlumeEmbeddingOptions options)
    {
        /// <summary>Configure Google Gemini as the primary embedding provider.</summary>
        public PlumeEmbeddingOptions UseGoogle(string apiKey, string? baseUrl = null)
            => options.Use(sp => CreateProvider(sp, apiKey, baseUrl));

        /// <summary>Configure Google Gemini as a fallback embedding provider.</summary>
        public PlumeEmbeddingOptions FallbackToGoogle(string apiKey, string? baseUrl = null)
            => options.AddFallback(sp => CreateProvider(sp, apiKey, baseUrl));
    }

    private static GoogleEmbeddingProvider CreateProvider(
        IServiceProvider sp,
        string apiKey,
        string? baseUrl)
    {
        var httpFactory = sp.GetService<IHttpClientFactory>();
        var http = httpFactory?.CreateClient("Plume.Google.Embeddings") ?? new HttpClient();

        if (http.Timeout == TimeSpan.FromSeconds(100)) // default
            http.Timeout = TimeSpan.FromMinutes(2);

        var providerOptions = new GoogleProviderOptions
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl ?? "https://generativelanguage.googleapis.com"
        };

        return new GoogleEmbeddingProvider(http, providerOptions);
    }
}
