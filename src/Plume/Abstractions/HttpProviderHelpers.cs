using System.Net;
using System.Net.Http.Headers;

namespace Plume.Abstractions;

/// <summary>
/// Shared helpers for HTTP-based providers. Maps HTTP status codes
/// to the right Plume exception types so failover can react correctly.
/// </summary>
public static class HttpProviderHelpers
{
    /// <summary>
    /// Throw the appropriate Plume exception for a non-success HTTP response.
    /// Parses Retry-After when present.
    /// </summary>
    public static async Task ThrowForNonSuccessAsync(
        string providerName,
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = string.Empty;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // ignore — best-effort
        }

        var status = (int)response.StatusCode;
        var summary = $"HTTP {status}: {response.ReasonPhrase}. {Truncate(body, 500)}";

        throw response.StatusCode switch
        {
            HttpStatusCode.TooManyRequests => new ProviderRateLimitException(providerName,
                GetRetryAfter(response.Headers.RetryAfter)),
            HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout
                or HttpStatusCode.RequestTimeout => new ProviderTransientException(providerName, summary),
            _ => new ProviderRequestException(providerName, summary, status)
        };
    }

    private static TimeSpan? GetRetryAfter(RetryConditionHeaderValue? header)
    {
        if (header is null) return null;
        if (header.Delta is { } delta) return delta;
        if (header.Date is { } date)
        {
            var diff = date - DateTimeOffset.UtcNow;
            return diff > TimeSpan.Zero ? diff : null;
        }
        return null;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "...");
}
