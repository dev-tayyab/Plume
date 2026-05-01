namespace Plume.Abstractions;

/// <summary>
/// The unified request shape that every provider receives.
/// Translation to provider-specific JSON happens inside the provider.
/// </summary>
public sealed record ProviderRequest
{
    /// <summary>The model identifier (e.g. "gpt-4o-mini" or "claude-sonnet-4").</summary>
    public required string Model { get; init; }

    /// <summary>Conversation messages in chronological order.</summary>
    public required IReadOnlyList<Message> Messages { get; init; }

    /// <summary>Sampling temperature.</summary>
    public double? Temperature { get; init; }

    /// <summary>Maximum tokens to generate.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>Stop sequences.</summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>
    /// Provider-specific extensions. Each provider casts to its own type
    /// (e.g. OpenAIProvider casts to OpenAIExtensions) and ignores extensions
    /// of other providers.
    /// </summary>
    public IProviderExtensions? Extensions { get; init; }
}
