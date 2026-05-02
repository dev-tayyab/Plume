using Microsoft.Extensions.DependencyInjection;

namespace Plume.OpenAI;

/// <summary>Extensions to register OpenAI as a Plume embedding provider.</summary>
public static class PlumeOpenAiEmbeddingsExtensions
{
    extension(PlumeEmbeddingOptions options)
    {
        /// <summary>Configure OpenAI as the primary embedding provider.</summary>
        public PlumeEmbeddingOptions UseOpenAi(string apiKey,
            string? baseUrl = null,
            string? organization = null,
            bool acceptAnyModel = false)
            => options.Use(sp => CreateProvider(sp, apiKey, baseUrl, organization, acceptAnyModel));

        /// <summary>Configure OpenAI as a fallback embedding provider.</summary>
        public PlumeEmbeddingOptions FallbackToOpenAi(string apiKey,
            string? baseUrl = null,
            string? organization = null,
            bool acceptAnyModel = false)
            => options.AddFallback(sp => CreateProvider(sp, apiKey, baseUrl, organization, acceptAnyModel));
    }

    private static OpenAiEmbeddingProvider CreateProvider(
        IServiceProvider sp,
        string apiKey,
        string? baseUrl,
        string? organization,
        bool acceptAnyModel)
    {
        var httpFactory = sp.GetService<IHttpClientFactory>();
        var http = httpFactory?.CreateClient("Plume.OpenAI.Embeddings") ?? new HttpClient();

        if (http.Timeout == TimeSpan.FromSeconds(100)) // default
            http.Timeout = TimeSpan.FromMinutes(2);

        var providerOptions = new OpenAiProviderOptions
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl ?? "https://api.openai.com",
            Organization = organization,
            AcceptAnyModel = acceptAnyModel
        };

        return new OpenAiEmbeddingProvider(http, providerOptions);
    }
}
