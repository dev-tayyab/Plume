namespace Plume;

/// <summary>
/// A multi-turn conversation. The session maintains message history,
/// so callers do not need to manage it.
/// </summary>
public interface IChatSession
{
    /// <summary>The full message history, including the system prompt if set.</summary>
    IReadOnlyList<Message> History { get; }

    /// <summary>Send a user message, get the assistant's reply, and update history.</summary>
    Task<string> AskAsync(string userMessage, CancellationToken ct = default);

    /// <summary>Streaming variant of <see cref="AskAsync"/>.</summary>
    IAsyncEnumerable<string> StreamAsync(string userMessage, CancellationToken ct = default);

    /// <summary>Manually append a message to the history (e.g., when restoring from storage).</summary>
    void AddMessage(Message message);

    /// <summary>Remove all messages except the system prompt.</summary>
    void Reset();
}
