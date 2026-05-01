namespace Plume;

/// <summary>
/// A single message in a conversation.
/// </summary>
public sealed record Message(MessageRole Role, string Content)
{
    /// <summary>When this message was created. Defaults to UTC now.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>The role of a message in a conversation.</summary>
public enum MessageRole
{
    /// <summary>System-level instructions to the model.</summary>
    System,

    /// <summary>A message from the user.</summary>
    User,

    /// <summary>A message from the model (assistant).</summary>
    Assistant,

    /// <summary>The result of a tool call (reserved for future use).</summary>
    Tool
}
