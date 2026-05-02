using Plume.Abstractions;
using Plume.Resilience;

namespace Plume;

/// <summary>
/// Static entry point for creating <see cref="IEmbeddingClient"/> instances
/// outside of dependency injection.
/// </summary>
public static class EmbeddingClient
{
    /// <summary>Create a fluent builder for setups including failover.</summary>
    public static EmbeddingClientBuilder CreateBuilder() => new();
}

/// <summary>
/// Fluent builder for <see cref="IEmbeddingClient"/>. Use <see cref="Use"/> for the primary
/// provider and <see cref="AddFallback"/> to add ordered fallbacks.
/// </summary>
public sealed class EmbeddingClientBuilder
{
    private readonly List<IEmbeddingProvider> _providers = new();
    private string? _defaultModel;
    private int? _defaultDimensions;

    /// <summary>Register a provider. The first call is the primary; later calls are fallbacks.</summary>
    public EmbeddingClientBuilder Use(IEmbeddingProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _providers.Add(provider);
        return this;
    }

    /// <summary>Register a fallback provider, used if earlier providers fail transiently.</summary>
    public EmbeddingClientBuilder AddFallback(IEmbeddingProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _providers.Add(provider);
        return this;
    }

    /// <summary>Set the default embedding model used when <see cref="EmbedOptions.Model"/> is not provided.</summary>
    public EmbeddingClientBuilder WithDefaultModel(string model)
    {
        _defaultModel = model;
        return this;
    }

    /// <summary>Set the default output dimensions for providers that support it.</summary>
    public EmbeddingClientBuilder WithDefaultDimensions(int dimensions)
    {
        _defaultDimensions = dimensions;
        return this;
    }

    /// <summary>Build the configured <see cref="IEmbeddingClient"/>.</summary>
    public IEmbeddingClient Build()
    {
        if (_providers.Count == 0)
            throw new InvalidOperationException(
                "At least one provider is required. Call Use(...) before Build().");

        IEmbeddingProvider effective = _providers.Count == 1
            ? _providers[0]
            : new FailoverEmbeddingProvider(_providers);

        var options = new PlumeEmbeddingOptions
        {
            DefaultModel = _defaultModel,
            DefaultDimensions = _defaultDimensions
        };

        return new DefaultEmbeddingClient(effective, options);
    }
}
