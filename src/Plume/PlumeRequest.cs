using Plume.Abstractions;

namespace Plume;

/// <summary>
/// A fully constructed request, used with <see cref="IPlumeClient.SendAsync"/>.
/// Most callers will use <see cref="IPlumeClient.AskAsync"/> instead.
/// </summary>
public sealed record PlumeRequest
{
    /// <summary>The conversation messages.</summary>
    public required IReadOnlyList<Message> Messages { get; init; }

    /// <summary>Optional model override.</summary>
    public string? Model { get; init; }

    /// <summary>Sampling temperature.</summary>
    public double? Temperature { get; init; }

    /// <summary>Maximum tokens to generate.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>Stop sequences.</summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>Strongly-typed provider-specific extensions.</summary>
    public IProviderExtensions? Extensions { get; init; }
}

/// <summary>The full response from <see cref="IPlumeClient.SendAsync"/>.</summary>
public sealed record PlumeResponse
{
    /// <summary>The model's text reply.</summary>
    public required string Content { get; init; }

    /// <summary>The model that produced this response.</summary>
    public required string Model { get; init; }

    /// <summary>The provider that produced this response.</summary>
    public required string Provider { get; init; }

    /// <summary>Why the model stopped generating.</summary>
    public required FinishReason FinishReason { get; init; }

    /// <summary>Token usage, if the provider reported it.</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>Provider-specific metadata.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}
