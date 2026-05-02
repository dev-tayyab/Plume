using Plume.Abstractions;
using Plume.Tools;

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

    /// <summary>
    /// Constrain the response to JSON (with optional schema enforcement).
    /// See <c>Plume.StructuredOutput.PlumeClientStructuredExtensions.AskAsync&lt;T&gt;</c>
    /// for the typed convenience API.
    /// </summary>
    public ResponseSchemaSpec? ResponseSchema { get; init; }

    /// <summary>Tools the model is permitted to call. Null disables tool use.</summary>
    public IReadOnlyList<Tool>? Tools { get; init; }

    /// <summary>How aggressively the model should use tools.</summary>
    public ToolChoice? ToolChoice { get; init; }

    /// <summary>Strongly-typed provider-specific extensions.</summary>
    public IProviderExtensions? Extensions { get; init; }
}

/// <summary>The full response from <see cref="IPlumeClient.SendAsync"/>.</summary>
public sealed record PlumeResponse
{
    /// <summary>The model's text reply. May be empty when <see cref="ToolCalls"/> is set.</summary>
    public required string Content { get; init; }

    /// <summary>The model that produced this response.</summary>
    public required string Model { get; init; }

    /// <summary>The provider that produced this response.</summary>
    public required string Provider { get; init; }

    /// <summary>Why the model stopped generating.</summary>
    public required FinishReason FinishReason { get; init; }

    /// <summary>Tool calls the model wants the caller to execute. Null when no tool was called.</summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>Token usage, if the provider reported it.</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>Provider-specific metadata.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}
