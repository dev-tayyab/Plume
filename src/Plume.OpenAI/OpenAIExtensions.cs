namespace Plume.OpenAI;

/// <summary>
/// OpenAI-specific request options. Pass via <see cref="AskOptions.Extensions"/>.
/// Ignored by other providers in a failover chain.
/// </summary>
public sealed record OpenAIExtensions : IProviderExtensions
{
    /// <summary>-2.0 to 2.0. Penalize tokens based on how often they appear so far.</summary>
    public double? FrequencyPenalty { get; init; }

    /// <summary>-2.0 to 2.0. Penalize new tokens based on whether they appear so far.</summary>
    public double? PresencePenalty { get; init; }

    /// <summary>If set, attempts deterministic sampling (best-effort).</summary>
    public int? Seed { get; init; }

    /// <summary>End-user identifier for OpenAI's abuse monitoring.</summary>
    public string? User { get; init; }

    /// <summary>Request a specific response format (e.g. JSON mode).</summary>
    public OpenAIResponseFormat? ResponseFormat { get; init; }

    /// <summary>Top-p nucleus sampling.</summary>
    public double? TopP { get; init; }
}

/// <summary>OpenAI response format options.</summary>
public enum OpenAIResponseFormat
{
    /// <summary>Default text response.</summary>
    Text,

    /// <summary>Force the response to be a valid JSON object.</summary>
    JsonObject
}
