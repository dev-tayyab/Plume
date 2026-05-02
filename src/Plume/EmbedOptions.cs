using Plume.Abstractions;

namespace Plume;

/// <summary>
/// Per-call options for <see cref="IEmbeddingClient.EmbedAsync(System.Collections.Generic.IReadOnlyList{string}, EmbedOptions?, System.Threading.CancellationToken)"/>.
/// </summary>
public sealed record EmbedOptions
{
    /// <summary>Override the default model for this call.</summary>
    public string? Model { get; init; }

    /// <summary>Requested output dimensions (provider-dependent — see <see cref="EmbeddingRequest.Dimensions"/>).</summary>
    public int? Dimensions { get; init; }

    /// <summary>Provider-specific extensions for this call.</summary>
    public IProviderExtensions? Extensions { get; init; }
}
