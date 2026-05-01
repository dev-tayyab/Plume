namespace Plume.Anthropic;

/// <summary>
/// Anthropic-specific request options. Pass via <see cref="AskOptions.Extensions"/>.
/// Ignored by other providers in a failover chain.
/// </summary>
public sealed record AnthropicExtensions : IProviderExtensions
{
    /// <summary>Top-K sampling. Default: provider chooses.</summary>
    public int? TopK { get; init; }

    /// <summary>Top-P nucleus sampling.</summary>
    public double? TopP { get; init; }

    /// <summary>Optional opaque user identifier (for Anthropic's abuse monitoring).</summary>
    public string? UserId { get; init; }
}
