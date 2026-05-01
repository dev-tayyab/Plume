namespace Plume.Abstractions;

/// <summary>
/// The seam between Plume's core and any LLM provider.
/// Implement this interface to add a new provider.
/// Streaming is opt-in via <see cref="IStreamingProvider"/>.
/// </summary>
public interface IProvider
{
    /// <summary>Identifier like "openai", "anthropic", "google", "ollama".</summary>
    string Name { get; }

    /// <summary>Quick check: does this provider know how to handle the requested model?</summary>
    bool Supports(string model);

    /// <summary>Send a request and await the full response.</summary>
    Task<ProviderResponse> SendAsync(ProviderRequest request, CancellationToken ct);
}

/// <summary>
/// A provider that supports server-sent streaming responses.
/// Implemented by all hosted providers shipping in v0.1.
/// </summary>
public interface IStreamingProvider : IProvider
{
    /// <summary>Stream a response as chunks arrive from the model.</summary>
    IAsyncEnumerable<ProviderStreamChunk> StreamAsync(
        ProviderRequest request,
        CancellationToken ct);
}
