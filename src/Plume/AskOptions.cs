namespace Plume;

/// <summary>
/// Per-call configuration for an LLM request. All members are optional;
/// when null, the client's defaults are used.
/// </summary>
public sealed record AskOptions
{
    /// <summary>Override the default model for this call.</summary>
    public string? Model { get; init; }

    /// <summary>Sampling temperature, typically 0.0 (deterministic) to 2.0 (very random).</summary>
    public double? Temperature { get; init; }

    /// <summary>Maximum tokens to generate.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>System prompt for one-shot calls.</summary>
    public string? System { get; init; }

    /// <summary>Stop sequences. Generation halts when any of these is produced.</summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>
    /// Provider-specific extensions (strongly typed per provider).
    /// Pass an instance like <c>OpenAIExtensions</c> or <c>AnthropicExtensions</c>.
    /// Extensions for a non-active provider are silently ignored — important
    /// for failover compatibility.
    /// </summary>
    public IProviderExtensions? Extensions { get; init; }
}

/// <summary>
/// Marker interface for provider-specific extension records.
/// Each provider package defines its own implementation
/// (e.g. <c>OpenAIExtensions</c>, <c>AnthropicExtensions</c>).
/// </summary>
public interface IProviderExtensions { }
