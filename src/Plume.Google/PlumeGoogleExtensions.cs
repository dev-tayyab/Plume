using Microsoft.Extensions.DependencyInjection;

namespace Plume.Google;

/// <summary>Extensions to register Google Gemini as a Plume provider.</summary>
public static class PlumeGoogleExtensions
{
    /// <summary>Configure Google Gemini as the primary provider.</summary>
    public static PlumeOptions UseGoogle(
        this PlumeOptions options,
        string apiKey,
        string? baseUrl = null,
        string? apiVersion = null)
        => options.Use(sp => CreateProvider(sp, apiKey, baseUrl, apiVersion));

    /// <summary>Configure Google Gemini as a fallback provider.</summary>
    public static PlumeOptions FallbackToGoogle(
        this PlumeOptions options,
        string apiKey,
        string? baseUrl = null,
        string? apiVersion = null)
        => options.AddFallback(sp => CreateProvider(sp, apiKey, baseUrl, apiVersion));

    private static GoogleProvider CreateProvider(
        IServiceProvider sp,
        string apiKey,
        string? baseUrl,
        string? apiVersion)
    {
        var httpFactory = sp.GetService<IHttpClientFactory>();
        var http = httpFactory?.CreateClient("Plume.Google") ?? new HttpClient();

        if (http.Timeout == TimeSpan.FromSeconds(100))
            http.Timeout = TimeSpan.FromMinutes(5);

        return new GoogleProvider(http, new GoogleProviderOptions
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl ?? "https://generativelanguage.googleapis.com",
            ApiVersion = apiVersion ?? "v1beta"
        });
    }
}
