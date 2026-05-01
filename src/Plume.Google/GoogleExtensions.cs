namespace Plume.Google;

/// <summary>
/// Google Gemini-specific request options. Pass via <see cref="AskOptions.Extensions"/>.
/// Ignored by other providers in a failover chain.
/// </summary>
public sealed record GoogleExtensions : IProviderExtensions
{
    /// <summary>Top-K sampling.</summary>
    public int? TopK { get; init; }

    /// <summary>Top-P nucleus sampling.</summary>
    public double? TopP { get; init; }

    /// <summary>Number of candidate responses to generate.</summary>
    public int? CandidateCount { get; init; }
}
