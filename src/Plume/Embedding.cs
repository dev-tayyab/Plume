namespace Plume;

/// <summary>
/// A single embedding vector returned by a model.
/// Wraps a <see cref="ReadOnlyMemory{T}"/> so callers can slice and reuse without copying.
/// </summary>
public sealed record Embedding
{
    /// <summary>Create an embedding from a vector.</summary>
    public Embedding(ReadOnlyMemory<float> vector)
    {
        Vector = vector;
    }

    /// <summary>The raw vector.</summary>
    public ReadOnlyMemory<float> Vector { get; }

    /// <summary>The number of dimensions in the vector.</summary>
    public int Dimensions => Vector.Length;

    /// <summary>
    /// Cosine similarity between this embedding and another in [-1, 1].
    /// Both vectors must have the same dimensionality.
    /// </summary>
    public float CosineSimilarity(Embedding other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return CosineSimilarity(Vector.Span, other.Vector.Span);
    }

    /// <summary>Cosine similarity between two raw vectors.</summary>
    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException(
                $"Vector dimensions must match ({a.Length} vs {b.Length}).", nameof(b));

        if (a.Length == 0) return 0f;

        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        var denom = Math.Sqrt(na) * Math.Sqrt(nb);
        return denom == 0 ? 0f : (float)(dot / denom);
    }
}
