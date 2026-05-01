using Microsoft.Extensions.DependencyInjection;

namespace Plume.Anthropic;

/// <summary>Extensions to register Anthropic as a Plume provider.</summary>
public static class PlumeAnthropicExtensions
{
    /// <summary>Configure Anthropic as the primary provider.</summary>
    public static PlumeOptions UseAnthropic(
        this PlumeOptions options,
        string apiKey,
        string? baseUrl = null,
        string? apiVersion = null)
        => options.Use(sp => CreateProvider(sp, apiKey, baseUrl, apiVersion));

    /// <summary>Configure Anthropic as a fallback provider.</summary>
    public static PlumeOptions FallbackToAnthropic(
        this PlumeOptions options,
        string apiKey,
        string? baseUrl = null,
        string? apiVersion = null)
        => options.AddFallback(sp => CreateProvider(sp, apiKey, baseUrl, apiVersion));

    private static AnthropicProvider CreateProvider(
        IServiceProvider sp,
        string apiKey,
        string? baseUrl,
        string? apiVersion)
    {
        var httpFactory = sp.GetService<IHttpClientFactory>();
        var http = httpFactory?.CreateClient("Plume.Anthropic") ?? new HttpClient();

        if (http.Timeout == TimeSpan.FromSeconds(100))
            http.Timeout = TimeSpan.FromMinutes(5); // long completions

        var providerOptions = new AnthropicProviderOptions
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl ?? "https://api.anthropic.com",
            ApiVersion = apiVersion ?? AnthropicProvider.DefaultApiVersion
        };

        return new AnthropicProvider(http, providerOptions);
    }
}
