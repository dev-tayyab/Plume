namespace Plume.Tools;

/// <summary>
/// A request from the model to invoke a tool. Returned in <c>ProviderResponse.ToolCalls</c>
/// and <see cref="Message.ToolCalls"/> on assistant messages.
/// </summary>
public sealed record ToolCall
{
    /// <summary>
    /// Provider-supplied identifier for this call. The corresponding tool-result
    /// message must echo this in <see cref="Message.ToolCallId"/>.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>The name of the tool being invoked.</summary>
    public required string Name { get; init; }

    /// <summary>The arguments JSON object as supplied by the model.</summary>
    public required string ArgumentsJson { get; init; }
}
