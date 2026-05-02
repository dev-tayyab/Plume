namespace Plume.Tools;

/// <summary>
/// How aggressively the model should use tools. Mapped to each provider's native
/// tool_choice format.
/// </summary>
public abstract record ToolChoice
{
    private ToolChoice() { }

    /// <summary>The model decides whether to call a tool (default).</summary>
    public sealed record Auto : ToolChoice;

    /// <summary>The model must call exactly one tool of its choosing.</summary>
    public sealed record Required : ToolChoice;

    /// <summary>The model must not call any tool.</summary>
    public sealed record None : ToolChoice;

    /// <summary>The model must call the named tool.</summary>
    public sealed record Specific(string Name) : ToolChoice;
}
