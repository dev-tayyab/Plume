using System.Text;
using Plume.Abstractions;
using Xunit;

namespace Plume.Tests;

public class SseEventReaderTests
{
    private static MemoryStream Stream(string text) =>
        new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static async Task<List<SseEvent>> ReadAll(string text)
    {
        var events = new List<SseEvent>();
        await foreach (var e in SseEventReader.ReadAsync(Stream(text), CancellationToken.None))
            events.Add(e);
        return events;
    }

    [Fact]
    public async Task ParsesSingleDataEvent()
    {
        var events = await ReadAll("data: hello\n\n");
        Assert.Single(events);
        Assert.Equal("hello", events[0].Data);
        Assert.Null(events[0].EventName);
    }

    [Fact]
    public async Task ParsesEventNameWithData()
    {
        var events = await ReadAll("event: message_start\ndata: {\"x\":1}\n\n");
        Assert.Single(events);
        Assert.Equal("message_start", events[0].EventName);
        Assert.Equal("{\"x\":1}", events[0].Data);
    }

    [Fact]
    public async Task ParsesMultipleEvents()
    {
        var events = await ReadAll("data: first\n\ndata: second\n\ndata: third\n\n");
        Assert.Equal(3, events.Count);
        Assert.Equal("first", events[0].Data);
        Assert.Equal("second", events[1].Data);
        Assert.Equal("third", events[2].Data);
    }

    [Fact]
    public async Task HandlesDoneSentinel()
    {
        var events = await ReadAll("data: hello\n\ndata: [DONE]\n\n");
        Assert.Equal(2, events.Count);
        Assert.Equal("[DONE]", events[1].Data);
    }

    [Fact]
    public async Task SkipsCommentLines()
    {
        var events = await ReadAll(": this is a comment\ndata: hello\n\n");
        Assert.Single(events);
        Assert.Equal("hello", events[0].Data);
    }

    [Fact]
    public async Task ConcatenatesMultipleDataLinesWithNewlines()
    {
        var events = await ReadAll("data: line1\ndata: line2\n\n");
        Assert.Single(events);
        Assert.Equal("line1\nline2", events[0].Data);
    }

    [Fact]
    public async Task HandlesAnthropicFormat()
    {
        var sse = "event: message_start\ndata: {\"type\":\"message_start\"}\n\n" +
                  "event: content_block_delta\ndata: {\"type\":\"content_block_delta\"}\n\n" +
                  "event: message_stop\ndata: {\"type\":\"message_stop\"}\n\n";

        var events = await ReadAll(sse);
        Assert.Equal(3, events.Count);
        Assert.Equal("message_start", events[0].EventName);
        Assert.Equal("content_block_delta", events[1].EventName);
        Assert.Equal("message_stop", events[2].EventName);
    }
}
