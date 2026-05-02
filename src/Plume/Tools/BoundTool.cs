namespace Plume.Tools;

/// <summary>
/// A <see cref="Tool"/> paired with a handler that the chat-session auto-loop
/// will invoke when the model calls the tool. Use <see cref="ToolBinder"/> to
/// construct one with strongly-typed args and results.
/// </summary>
public sealed class BoundTool
{
    /// <summary>The tool metadata exposed to the model.</summary>
    public Tool Tool { get; }

    /// <summary>
    /// Handler that runs the tool. Receives the raw arguments JSON (as the model
    /// produced it) and returns the result string (typically JSON, included verbatim
    /// in the tool-result message sent back to the model).
    /// </summary>
    public Func<string, CancellationToken, Task<string>> Handler { get; }

    /// <summary>Create a BoundTool from raw metadata and a string-in / string-out handler.</summary>
    public BoundTool(Tool tool, Func<string, CancellationToken, Task<string>> handler)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(handler);
        Tool = tool;
        Handler = handler;
    }
}
