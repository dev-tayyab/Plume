using Microsoft.Extensions.Logging;
using Plume.Abstractions;

namespace Plume.Resilience;

/// <summary>
/// An <see cref="IEmbeddingProvider"/> that delegates to an ordered list of providers.
/// On a transient failure, falls through to the next.
/// </summary>
internal sealed partial class FailoverEmbeddingProvider : IEmbeddingProvider
{
    private readonly IReadOnlyList<IEmbeddingProvider> _providers;
    private readonly ILogger<FailoverEmbeddingProvider>? _logger;

    public FailoverEmbeddingProvider(
        IReadOnlyList<IEmbeddingProvider> providers,
        ILogger<FailoverEmbeddingProvider>? logger = null)
    {
        if (providers is null || providers.Count == 0)
            throw new ArgumentException("At least one provider is required.", nameof(providers));

        _providers = providers;
        _logger = logger;
    }

    public string Name => "failover";

    public bool Supports(string model) => _providers.Any(p => p.Supports(model));

    public async Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken ct)
    {
        var errors = new List<Exception>();

        foreach (var provider in _providers)
        {
            ct.ThrowIfCancellationRequested();

            if (!provider.Supports(request.Model))
            {
                if (_logger is not null) LogSkippedModel(_logger, provider.Name, request.Model);
                continue;
            }

            try
            {
                return await provider.EmbedAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                if (_logger is not null) LogProviderFailed(_logger, ex, provider.Name);
                errors.Add(ex);
            }
        }

        throw new AllProvidersFailedException(errors);
    }

    private static bool IsTransient(Exception ex) => ex switch
    {
        ProviderRateLimitException => true,
        ProviderTransientException => true,
        HttpRequestException => true,
        TaskCanceledException tc when tc.CancellationToken == default => true,
        TimeoutException => true,
        _ => false
    };

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Skipping {Provider}: does not support embedding model {Model}")]
    private static partial void LogSkippedModel(ILogger logger, string provider, string model);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Embedding provider {Provider} failed transiently, falling back")]
    private static partial void LogProviderFailed(ILogger logger, Exception ex, string provider);
}
