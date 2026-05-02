using Plume.Tools;

namespace Plume;

/// <summary>
/// A single message in a conversation.
/// </summary>
public sealed record Message(MessageRole Role, string Content)
{
    /// <summary>When this message was created. Defaults to UTC now.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Tool calls requested by the model on an <see cref="MessageRole.Assistant"/>
    /// message. Null on user/system/tool messages.
    /// </summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>
    /// On a <see cref="MessageRole.Tool"/> message, the id of the call this is
    /// the result for. Null on other message kinds.
    /// </summary>
    public string? ToolCallId { get; init; }
}

/// <summary>The role of a message in a conversation.</summary>
public enum MessageRole
{
    /// <summary>System-level instructions to the model.</summary>
    System,

    /// <summary>A message from the user.</summary>
    User,

    /// <summary>A message from the model (assistant), possibly carrying tool calls.</summary>
    Assistant,

    /// <summary>The result of a tool call sent back to the model.</summary>
    Tool
}
