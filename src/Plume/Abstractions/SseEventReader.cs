using System.Runtime.CompilerServices;

namespace Plume.Abstractions;

/// <summary>
/// A minimal Server-Sent Events parser for use by provider implementations.
/// Reads events from a stream and yields <see cref="SseEvent"/> records.
/// </summary>
/// <remarks>
/// SSE format (RFC):
/// <code>
/// event: message_start
/// data: {"type": "..."}
///
/// data: {"type": "..."}
///
/// </code>
/// Events are separated by blank lines. Each event may have an optional
/// <c>event:</c> name and one or more <c>data:</c> lines (concatenated with newlines).
/// </remarks>
public static class SseEventReader
{
    /// <summary>
    /// Read SSE events from a stream until it ends or is canceled.
    /// </summary>
    public static async IAsyncEnumerable<SseEvent> ReadAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream);
        string? eventName = null;
        var dataBuffer = new System.Text.StringBuilder();

        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();

            // Empty line — dispatch the accumulated event.
            if (line.Length == 0)
            {
                if (dataBuffer.Length > 0 || eventName is not null)
                {
                    yield return new SseEvent(eventName, dataBuffer.ToString());
                    dataBuffer.Clear();
                    eventName = null;
                }
                continue;
            }

            // Comment / keep-alive
            if (line.StartsWith(':'))
                continue;

            var colonIndex = line.IndexOf(':');
            string field, value;

            if (colonIndex < 0)
            {
                field = line;
                value = string.Empty;
            }
            else
            {
                field = line[..colonIndex];
                value = line[(colonIndex + 1)..];
                // Per spec, leading single space after colon is stripped.
                if (value.StartsWith(' '))
                    value = value[1..];
            }

            switch (field)
            {
                case "event":
                    eventName = value;
                    break;
                case "data":
                    if (dataBuffer.Length > 0)
                        dataBuffer.Append('\n');
                    dataBuffer.Append(value);
                    break;
                // Other fields ("id", "retry") are ignored — we don't need them for LLM streaming.
            }
        }

        // Final event without trailing blank line
        if (dataBuffer.Length > 0 || eventName is not null)
            yield return new SseEvent(eventName, dataBuffer.ToString());
    }
}

/// <summary>One server-sent event.</summary>
public readonly record struct SseEvent(string? EventName, string Data);
