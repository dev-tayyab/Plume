namespace Plume.Tools;

/// <summary>
/// Metadata describing a tool the model can call. Pair with a handler via
/// <see cref="ToolBinder"/> to register with a chat session.
/// </summary>
public sealed record Tool
{
    /// <summary>
    /// The tool's name. Must be unique within a request and match
    /// <c>^[a-zA-Z0-9_-]{1,64}$</c> (per OpenAI's constraint, the strictest).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Human description shown to the model — keep it concise and action-oriented.</summary>
    public string? Description { get; init; }

    /// <summary>JSON Schema describing the tool's input arguments.</summary>
    public required string ParametersJsonSchema { get; init; }
}
