using Plume.Abstractions;
using Plume.Resilience;

namespace Plume;

/// <summary>
/// Static entry point for creating <see cref="IPlumeClient"/> instances
/// outside of dependency injection.
/// </summary>
public static class PlumeClient
{
    /// <summary>
    /// Create a fluent builder for setups including failover and retry.
    /// </summary>
    public static PlumeClientBuilder CreateBuilder() => new();
}

/// <summary>
/// Fluent builder for <see cref="IPlumeClient"/>. Use <see cref="Use"/> for the primary
/// provider and <see cref="AddFallback"/> to add ordered fallbacks.
/// </summary>
public sealed class PlumeClientBuilder
{
    private readonly List<IProvider> _providers = new();
    private string? _defaultModel;
    private string? _defaultSystemPrompt;
    private double? _defaultTemperature;
    private int? _defaultMaxTokens;

    /// <summary>Register a provider. The first call is the primary; later calls are fallbacks.</summary>
    public PlumeClientBuilder Use(IProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _providers.Add(provider);
        return this;
    }

    /// <summary>Register a fallback provider, used if earlier providers fail transiently.</summary>
    public PlumeClientBuilder AddFallback(IProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _providers.Add(provider);
        return this;
    }

    /// <summary>Set the default model used when AskOptions.Model isn't provided.</summary>
    public PlumeClientBuilder WithDefaultModel(string model)
    {
        _defaultModel = model;
        return this;
    }

    /// <summary>Set the default system prompt prepended to one-shot calls.</summary>
    public PlumeClientBuilder WithDefaultSystem(string system)
    {
        _defaultSystemPrompt = system;
        return this;
    }

    /// <summary>Set the default sampling temperature.</summary>
    public PlumeClientBuilder WithDefaultTemperature(double temperature)
    {
        _defaultTemperature = temperature;
        return this;
    }

    /// <summary>Set the default max tokens.</summary>
    public PlumeClientBuilder WithDefaultMaxTokens(int maxTokens)
    {
        _defaultMaxTokens = maxTokens;
        return this;
    }

    /// <summary>Build the configured <see cref="IPlumeClient"/>.</summary>
    public IPlumeClient Build()
    {
        if (_providers.Count == 0)
            throw new InvalidOperationException(
                "At least one provider is required. Call Use(...) before Build().");

        IProvider effective = _providers.Count == 1
            ? _providers[0]
            : new FailoverProvider(_providers);

        var options = new PlumeOptions
        {
            DefaultModel = _defaultModel,
            DefaultSystemPrompt = _defaultSystemPrompt,
            DefaultTemperature = _defaultTemperature,
            DefaultMaxTokens = _defaultMaxTokens
        };

        return new DefaultPlumeClient(effective, options);
    }
}
