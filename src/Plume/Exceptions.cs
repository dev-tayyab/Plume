namespace Plume;

/// <summary>Base class for Plume-specific exceptions.</summary>
public abstract class PlumeException : Exception
{
    /// <summary>Initializes the exception with a message.</summary>
    protected PlumeException(string message) : base(message) { }

    /// <summary>Initializes the exception with a message and inner exception.</summary>
    protected PlumeException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when a request fails and no provider in the failover chain succeeds.
/// </summary>
public sealed class AllProvidersFailedException : PlumeException
{
    /// <summary>The errors from each provider attempt, in order.</summary>
    public IReadOnlyList<Exception> ProviderErrors { get; }

    /// <summary>Creates a new <see cref="AllProvidersFailedException"/>.</summary>
    public AllProvidersFailedException(IReadOnlyList<Exception> errors)
        : base($"All {errors.Count} provider(s) failed. See ProviderErrors for details.")
    {
        ProviderErrors = errors;
    }
}

/// <summary>
/// Thrown when a provider returns a transient error (HTTP 503, network, etc.).
/// Recognized by failover logic as a fallback signal.
/// </summary>
public class ProviderTransientException : PlumeException
{
    /// <summary>The provider that failed.</summary>
    public string ProviderName { get; }

    /// <summary>Creates a new transient exception.</summary>
    public ProviderTransientException(string provider, string message)
        : base($"[{provider}] {message}")
    {
        ProviderName = provider;
    }

    /// <summary>Creates a new transient exception with an inner exception.</summary>
    public ProviderTransientException(string provider, string message, Exception inner)
        : base($"[{provider}] {message}", inner)
    {
        ProviderName = provider;
    }
}

/// <summary>Thrown when a provider rate-limits the request (HTTP 429).</summary>
public sealed class ProviderRateLimitException : ProviderTransientException
{
    /// <summary>If the provider sent a Retry-After hint, that value.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>Creates a rate-limit exception.</summary>
    public ProviderRateLimitException(string provider, TimeSpan? retryAfter = null)
        : base(provider, "Rate limit exceeded.")
    {
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Thrown when a provider returns a non-transient error (auth, bad request, etc.).
/// Failover does NOT retry on these — the request is fundamentally bad.
/// </summary>
public sealed class ProviderRequestException : PlumeException
{
    /// <summary>The provider that failed.</summary>
    public string ProviderName { get; }

    /// <summary>The HTTP status code, if applicable.</summary>
    public int? StatusCode { get; }

    /// <summary>Creates a request exception.</summary>
    public ProviderRequestException(string provider, string message, int? statusCode = null)
        : base($"[{provider}] {message}")
    {
        ProviderName = provider;
        StatusCode = statusCode;
    }
}
