using Plume.Tools;

namespace Plume.Abstractions;

/// <summary>The unified response shape returned by any provider.</summary>
public sealed record ProviderResponse
{
    /// <summary>The text content the model produced. May be empty when <see cref="ToolCalls"/> is set.</summary>
    public required string Content { get; init; }

    /// <summary>
    /// The model that produced this response. May differ from the request
    /// if a fallback was used.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>Why the model stopped generating.</summary>
    public required FinishReason FinishReason { get; init; }

    /// <summary>Tool calls the model wants the caller to execute. Null when no tool was called.</summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>Token usage. Null if the provider didn't report it.</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>Provider-specific metadata (request ID, fingerprint, etc.).</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}

/// <summary>One chunk of a streamed response.</summary>
public sealed record ProviderStreamChunk
{
    /// <summary>The text fragment in this chunk. Maybe empty for keep-alive chunks.</summary>
    public required string Content { get; init; }

    /// <summary>True only on the final chunk of a stream.</summary>
    public bool IsFinal { get; init; }

    /// <summary>Token usage. Typically only populated on the final chunk.</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>Finish reason. Typically only populated on the final chunk.</summary>
    public FinishReason? FinishReason { get; init; }
}

/// <summary>Reason the model stopped generating tokens.</summary>
public enum FinishReason
{
    /// <summary>Natural stop or stop sequence hit.</summary>
    Stop,

    /// <summary>Reached MaxTokens.</summary>
    Length,

    /// <summary>The provider's content filter intercepted the response.</summary>
    ContentFilter,

    /// <summary>An error occurred mid-generation.</summary>
    Error,

    /// <summary>The model stopped to request tool calls.</summary>
    ToolCalls,

    /// <summary>Provider returned a reason Plume doesn't recognize.</summary>
    Other
}

/// <summary>Token usage for a single request.</summary>
public sealed record TokenUsage(int PromptTokens, int CompletionTokens)
{
    /// <summary>Sum of prompt and completion tokens.</summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}
