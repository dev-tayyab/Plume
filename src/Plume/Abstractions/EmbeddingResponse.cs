namespace Plume.Abstractions;

/// <summary>The unified response shape returned by any <see cref="IEmbeddingProvider"/>.</summary>
public sealed record EmbeddingResponse
{
    /// <summary>The vectors, in the same order as <see cref="EmbeddingRequest.Inputs"/>.</summary>
    public required IReadOnlyList<Embedding> Embeddings { get; init; }

    /// <summary>The model that produced the response. May differ from the request if a fallback was used.</summary>
    public required string Model { get; init; }

    /// <summary>Token usage. Null if the provider didn't report it.</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>Provider-specific metadata.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}
