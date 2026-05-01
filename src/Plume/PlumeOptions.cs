using Plume.Abstractions;

namespace Plume;

/// <summary>
/// Configuration for an <see cref="IPlumeClient"/>.
/// Use <see cref="Use"/> for the primary provider and <see cref="AddFallback"/>
/// for ordered fallbacks.
/// </summary>
public sealed class PlumeOptions
{
    private readonly List<Func<IServiceProvider, IProvider>> _providerFactories = new();

    /// <summary>The default model to use when <see cref="AskOptions.Model"/> is not specified.</summary>
    public string? DefaultModel { get; set; }

    /// <summary>The default system prompt prepended to one-shot calls.</summary>
    public string? DefaultSystemPrompt { get; set; }

    /// <summary>Default sampling temperature.</summary>
    public double? DefaultTemperature { get; set; }

    /// <summary>Default max tokens.</summary>
    public int? DefaultMaxTokens { get; set; }

    /// <summary>Internal: registered provider factories in order (primary first, fallbacks after).</summary>
    internal IReadOnlyList<Func<IServiceProvider, IProvider>> ProviderFactories => _providerFactories;

    /// <summary>Register a provider. The first call is the primary; later calls are fallbacks.</summary>
    public PlumeOptions Use(Func<IServiceProvider, IProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _providerFactories.Add(factory);
        return this;
    }

    /// <summary>Register a fallback provider. Equivalent to <see cref="Use"/> after the first call.</summary>
    public PlumeOptions AddFallback(Func<IServiceProvider, IProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _providerFactories.Add(factory);
        return this;
    }
}
