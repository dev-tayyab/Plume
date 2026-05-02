using Plume.Abstractions;

namespace Plume;

/// <summary>
/// Configuration for an <see cref="IEmbeddingClient"/>.
/// Use <see cref="Use"/> for the primary provider and <see cref="AddFallback"/>
/// for ordered fallbacks.
/// </summary>
public sealed class PlumeEmbeddingOptions
{
    private readonly List<Func<IServiceProvider, IEmbeddingProvider>> _providerFactories = new();

    /// <summary>The default embedding model used when <see cref="EmbedOptions.Model"/> is not specified.</summary>
    public string? DefaultModel { get; set; }

    /// <summary>Default output dimensions (provider-dependent).</summary>
    public int? DefaultDimensions { get; set; }

    /// <summary>Internal: registered provider factories in order (primary first, fallbacks after).</summary>
    internal IReadOnlyList<Func<IServiceProvider, IEmbeddingProvider>> ProviderFactories => _providerFactories;

    /// <summary>Register a provider. The first call is the primary; later calls are fallbacks.</summary>
    public PlumeEmbeddingOptions Use(Func<IServiceProvider, IEmbeddingProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _providerFactories.Add(factory);
        return this;
    }

    /// <summary>Register a fallback provider. Equivalent to <see cref="Use"/> after the first call.</summary>
    public PlumeEmbeddingOptions AddFallback(Func<IServiceProvider, IEmbeddingProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _providerFactories.Add(factory);
        return this;
    }
}
