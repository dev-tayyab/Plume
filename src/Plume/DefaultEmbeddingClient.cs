using Plume.Abstractions;

namespace Plume;

/// <summary>
/// Default implementation of <see cref="IEmbeddingClient"/>. Maps the public
/// API onto provider-agnostic <see cref="EmbeddingRequest"/>s.
/// </summary>
internal sealed class DefaultEmbeddingClient(
    IEmbeddingProvider provider,
    PlumeEmbeddingOptions options) : IEmbeddingClient
{
    private readonly IEmbeddingProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    private readonly PlumeEmbeddingOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async Task<IReadOnlyList<Embedding>> EmbedAsync(
        IReadOnlyList<string> inputs,
        EmbedOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count == 0)
            throw new ArgumentException("At least one input is required.", nameof(inputs));

        var request = new EmbeddingRequest
        {
            Model = options?.Model ?? _options.DefaultModel
                ?? throw new InvalidOperationException(
                    "No model specified. Set EmbedOptions.Model or PlumeEmbeddingOptions.DefaultModel."),
            Inputs = inputs,
            Dimensions = options?.Dimensions ?? _options.DefaultDimensions,
            Extensions = options?.Extensions
        };

        var response = await _provider.EmbedAsync(request, ct).ConfigureAwait(false);
        return response.Embeddings;
    }

    public Task<EmbeddingResponse> SendAsync(EmbeddingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _provider.EmbedAsync(request, ct);
    }
}
