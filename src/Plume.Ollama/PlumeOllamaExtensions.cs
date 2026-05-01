using Microsoft.Extensions.DependencyInjection;

namespace Plume.Ollama;

/// <summary>Extensions to register Ollama as a Plume provider.</summary>
public static class PlumeOllamaExtensions
{
    /// <summary>Configure Ollama as the primary provider.</summary>
    public static PlumeOptions UseOllama(
        this PlumeOptions options,
        string? baseUrl = null,
        string? requiredModelPrefix = null)
        => options.Use(sp => CreateProvider(sp, baseUrl, requiredModelPrefix));

    /// <summary>Configure Ollama as a fallback provider (e.g. local last resort).</summary>
    public static PlumeOptions FallbackToOllama(
        this PlumeOptions options,
        string? baseUrl = null,
        string? requiredModelPrefix = null)
        => options.AddFallback(sp => CreateProvider(sp, baseUrl, requiredModelPrefix));

    private static OllamaProvider CreateProvider(
        IServiceProvider sp,
        string? baseUrl,
        string? requiredModelPrefix)
    {
        var httpFactory = sp.GetService<IHttpClientFactory>();
        var http = httpFactory?.CreateClient("Plume.Ollama") ?? new HttpClient();

        if (http.Timeout == TimeSpan.FromSeconds(100))
            http.Timeout = TimeSpan.FromMinutes(10); // local models can be slow

        return new OllamaProvider(http, new OllamaProviderOptions
        {
            BaseUrl = baseUrl ?? "http://localhost:11434",
            RequiredModelPrefix = requiredModelPrefix
        });
    }
}
