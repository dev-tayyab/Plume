using Xunit;

namespace Plume.Tests;

public class ChatSessionTests
{
    private static IPlumeClient ClientWith(FakeProvider provider) =>
        PlumeClient.CreateBuilder()
            .WithDefaultModel("test")
            .Use(provider)
            .Build();

    [Fact]
    public async Task MaintainsHistoryAcrossTurns()
    {
        var p = new FakeProvider("p");
        p.QueueResponse("hi back");
        p.QueueResponse("doing well");

        var client = ClientWith(p);
        var chat = client.NewChat(system: "You are nice.");

        var first = await chat.AskAsync("hi");
        var second = await chat.AskAsync("how are you?");

        Assert.Equal("hi back", first);
        Assert.Equal("doing well", second);

        // System + 2x(user, assistant) = 5 messages
        Assert.Equal(5, chat.History.Count);
        Assert.Equal(MessageRole.System, chat.History[0].Role);
        Assert.Equal("You are nice.", chat.History[0].Content);
        Assert.Equal(MessageRole.User, chat.History[1].Role);
        Assert.Equal("hi", chat.History[1].Content);
        Assert.Equal(MessageRole.Assistant, chat.History[2].Role);
        Assert.Equal("hi back", chat.History[2].Content);
    }

    [Fact]
    public async Task ResetKeepsSystemMessage()
    {
        var p = new FakeProvider("p");
        p.QueueResponse("a");

        var client = ClientWith(p);
        var chat = client.NewChat(system: "system prompt");

        await chat.AskAsync("question");
        Assert.Equal(3, chat.History.Count);

        chat.Reset();
        Assert.Single(chat.History);
        Assert.Equal(MessageRole.System, chat.History[0].Role);
    }

    [Fact]
    public async Task ResetWithNoSystemClearsAll()
    {
        var p = new FakeProvider("p");
        p.QueueResponse("a");

        var client = ClientWith(p);
        var chat = client.NewChat();

        await chat.AskAsync("question");
        Assert.Equal(2, chat.History.Count);

        chat.Reset();
        Assert.Empty(chat.History);
    }

    [Fact]
    public async Task StreamingAggregatesChunksIntoHistory()
    {
        var p = new FakeProvider("p");
        p.QueueStream("Hel", "lo ", "world");

        var client = ClientWith(p);
        var chat = client.NewChat();

        var collected = new List<string>();
        await foreach (var c in chat.StreamAsync("hi"))
            collected.Add(c);

        Assert.Equal("Hello world", string.Concat(collected));
        Assert.Equal(2, chat.History.Count); // user + assistant
        Assert.Equal("Hello world", chat.History[1].Content);
    }

    [Fact]
    public void AddMessageAppendsToHistory()
    {
        var p = new FakeProvider("p");
        var client = ClientWith(p);
        var chat = client.NewChat();

        chat.AddMessage(new Message(MessageRole.User, "first"));
        chat.AddMessage(new Message(MessageRole.Assistant, "reply"));

        Assert.Equal(2, chat.History.Count);
    }
}
