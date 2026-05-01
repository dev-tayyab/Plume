namespace Plume.Ollama;

/// <summary>
/// Ollama-specific request options. Pass via <see cref="AskOptions.Extensions"/>.
/// Ignored by other providers in a failover chain.
/// </summary>
public sealed record OllamaExtensions : IProviderExtensions
{
    /// <summary>Top-K sampling.</summary>
    public int? TopK { get; init; }

    /// <summary>Top-P nucleus sampling.</summary>
    public double? TopP { get; init; }

    /// <summary>Random seed for reproducibility.</summary>
    public int? Seed { get; init; }

    /// <summary>How long the model should stay loaded after the request, e.g. "5m" or "0".</summary>
    public string? KeepAlive { get; init; }
}
