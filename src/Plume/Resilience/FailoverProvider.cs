using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Plume.Abstractions;

namespace Plume.Resilience;

/// <summary>
/// An <see cref="IStreamingProvider"/> that delegates to an ordered list of providers.
/// On a transient failure of one provider, falls through to the next.
/// Itself implements <see cref="IStreamingProvider"/> so it composes anywhere a provider is expected.
/// </summary>
internal sealed partial class FailoverProvider : IStreamingProvider
{
    private readonly IReadOnlyList<IProvider> _providers;
    private readonly ILogger<FailoverProvider>? _logger;

    public FailoverProvider(
        IReadOnlyList<IProvider> providers,
        ILogger<FailoverProvider>? logger = null)
    {
        if (providers is null || providers.Count == 0)
            throw new ArgumentException("At least one provider is required.", nameof(providers));

        _providers = providers;
        _logger = logger;
    }

    public string Name => "failover";

    public bool Supports(string model) => _providers.Any(p => p.Supports(model));

    public async Task<ProviderResponse> SendAsync(
        ProviderRequest request, CancellationToken ct)
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
                return await provider.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                if (_logger is not null) LogProviderFailed(_logger, ex, provider.Name);
                errors.Add(ex);
            }
        }

        throw new AllProvidersFailedException(errors);
    }

    public async IAsyncEnumerable<ProviderStreamChunk> StreamAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var errors = new List<Exception>();

        foreach (var provider in _providers)
        {
            ct.ThrowIfCancellationRequested();

            if (provider is not IStreamingProvider streaming)
            {
                if (_logger is not null) LogSkippedStreaming(_logger, provider.Name);
                continue;
            }

            if (!provider.Supports(request.Model))
                continue;

            // Try to get the enumerator and pull the first chunk.
            // If that fails transiently, we can still fall back.
            // Once we yield even one chunk, we cannot fall back —
            // the caller has already received partial output.
            //
            // C# does not allow `yield return` inside a try-with-catch,
            // so the try/catch lives in a non-iterator helper.
            var attempt = await TryStartStreamAsync(streaming, request, ct).ConfigureAwait(false);

            if (attempt.Error is not null)
            {
                errors.Add(attempt.Error);
                continue;
            }

            if (attempt.Enumerator is null)
            {
                // Empty stream from a healthy provider — treat as success.
                yield break;
            }

            // First chunk succeeded; yield everything from this provider.
            // try/finally is allowed in iterators (only try/catch is not).
            var enumerator = attempt.Enumerator;
            try
            {
                yield return enumerator.Current;
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    yield return enumerator.Current;
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }

            yield break;
        }

        throw new AllProvidersFailedException(errors);
    }

    /// <summary>
    /// Helper that lives outside the iterator so we can use try/catch.
    /// Returns either an enumerator positioned at the first chunk, or an error,
    /// or (Enumerator: null, Error: null) for an empty stream.
    /// </summary>
    private async Task<StreamStartAttempt> TryStartStreamAsync(
        IStreamingProvider provider,
        ProviderRequest request,
        CancellationToken ct)
    {
        IAsyncEnumerator<ProviderStreamChunk>? enumerator = null;
        try
        {
            enumerator = provider.StreamAsync(request, ct).GetAsyncEnumerator(ct);
            if (await enumerator.MoveNextAsync().ConfigureAwait(false)) return new StreamStartAttempt(enumerator, null);
            await enumerator.DisposeAsync().ConfigureAwait(false);
            return new StreamStartAttempt(null, null);
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            if (_logger is not null) LogStreamProviderFailed(_logger, ex, provider.Name);
            if (enumerator is null) return new StreamStartAttempt(null, ex);
            try { await enumerator.DisposeAsync().ConfigureAwait(false); }
            catch { /* swallow disposal errors */ }
            return new StreamStartAttempt(null, ex);
        }
    }

    private readonly record struct StreamStartAttempt(
        IAsyncEnumerator<ProviderStreamChunk>? Enumerator,
        Exception? Error);

    /// <summary>
    /// Decide whether a failure should trigger fallback. Auth or bad-request
    /// errors are NOT transient — failover won't help them.
    /// </summary>
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
        Message = "Skipping {Provider}: does not support model {Model}")]
    private static partial void LogSkippedModel(ILogger logger, string provider, string model);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Skipping {Provider}: does not support streaming")]
    private static partial void LogSkippedStreaming(ILogger logger, string provider);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Provider {Provider} failed transiently, falling back")]
    private static partial void LogProviderFailed(ILogger logger, Exception ex, string provider);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Streaming provider {Provider} failed before first chunk, falling back")]
    private static partial void LogStreamProviderFailed(ILogger logger, Exception ex, string provider);
}
