namespace Plume;

/// <summary>
/// The main entry point for talking to Large Language Models with Plume.
/// One method per use case, no ceremony.
/// </summary>
public interface IPlumeClient
{
    /// <summary>
    /// Send a single prompt and get the model's text response.
    /// </summary>
    Task<string> AskAsync(
        string prompt,
        AskOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Stream the model's response as it arrives, token by token.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The active provider does not support streaming.
    /// </exception>
    IAsyncEnumerable<string> StreamAsync(
        string prompt,
        AskOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Start a multi-turn conversation. The session keeps history so the
    /// caller does not need to manage it.
    /// </summary>
    IChatSession NewChat(string? system = null, AskOptions? options = null);

    /// <summary>
    /// Lower-level escape hatch: send a fully constructed request and
    /// receive the full response with metadata (usage, finish reason, etc.).
    /// </summary>
    Task<PlumeResponse> SendAsync(
        PlumeRequest request,
        CancellationToken ct = default);
}
