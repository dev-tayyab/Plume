using Plume.Tools;

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

    /// <summary>
    /// Register tools available to the model. After registration, <see cref="AskAsync"/>
    /// runs the call loop automatically: when the model emits tool calls, each handler
    /// is invoked, results are appended to history, and the request is replayed until
    /// the model stops calling tools or <see cref="MaxToolIterations"/> is reached.
    /// </summary>
    IChatSession UseTools(params BoundTool[] tools);

    /// <summary>
    /// Maximum number of tool-call rounds in a single <see cref="AskAsync"/> invocation.
    /// Defaults to 10. Set to a smaller value to fail fast on runaway loops.
    /// </summary>
    int MaxToolIterations { get; set; }
}
