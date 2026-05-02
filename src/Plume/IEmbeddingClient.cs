using Plume.Abstractions;

namespace Plume;

/// <summary>
/// High-level entry point for generating text embeddings with Plume.
/// Failover-aware: configure multiple providers via <see cref="EmbeddingClient.CreateBuilder"/>
/// and Plume will retry transient failures across them.
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>Embed a batch of inputs.</summary>
    Task<IReadOnlyList<Embedding>> EmbedAsync(
        IReadOnlyList<string> inputs,
        EmbedOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lower-level escape hatch: send a fully constructed request and receive the
    /// full response with metadata (usage, model fingerprint, etc.).
    /// </summary>
    Task<EmbeddingResponse> SendAsync(
        EmbeddingRequest request,
        CancellationToken ct = default);
}

/// <summary>Convenience extensions over <see cref="IEmbeddingClient"/>.</summary>
public static class EmbeddingClientExtensions
{
    /// <summary>Embed a single input. Returns the resulting vector.</summary>
    public static async Task<Embedding> EmbedAsync(
        this IEmbeddingClient client,
        string input,
        EmbedOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrEmpty(input);

        var batch = await client.EmbedAsync(new[] { input }, options, ct).ConfigureAwait(false);
        return batch[0];
    }
}
