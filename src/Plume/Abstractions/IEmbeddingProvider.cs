namespace Plume.Abstractions;

/// <summary>
/// Provider seam for text embedding models.
/// Mirrors <see cref="IProvider"/> but produces vector embeddings rather than text completions.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>Identifier like "openai", "google", "ollama".</summary>
    string Name { get; }

    /// <summary>Quick check: does this provider know how to handle the requested embedding model?</summary>
    bool Supports(string model);

    /// <summary>Embed one or more inputs and await the vectors.</summary>
    Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken ct);
}
