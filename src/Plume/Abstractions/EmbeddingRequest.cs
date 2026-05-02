namespace Plume.Abstractions;

/// <summary>
/// The unified embedding request shape that every <see cref="IEmbeddingProvider"/> receives.
/// Translation to provider-specific JSON happens inside the provider.
/// </summary>
public sealed record EmbeddingRequest
{
    /// <summary>The embedding model identifier (e.g. "text-embedding-3-small" or "nomic-embed-text").</summary>
    public required string Model { get; init; }

    /// <summary>One or more inputs to embed. Each becomes a separate vector in the response.</summary>
    public required IReadOnlyList<string> Inputs { get; init; }

    /// <summary>
    /// Requested output dimensions. Honored by providers that support truncation
    /// (e.g. OpenAI text-embedding-3-*). Null means use the model default.
    /// </summary>
    public int? Dimensions { get; init; }

    /// <summary>
    /// Provider-specific extensions. Each provider casts to its own type
    /// and ignores extensions of other providers.
    /// </summary>
    public IProviderExtensions? Extensions { get; init; }
}
