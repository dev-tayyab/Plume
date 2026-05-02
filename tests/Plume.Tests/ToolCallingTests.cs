using System.Text.Json;
using System.Text.Json.Serialization;
using Plume.Tools;
using Xunit;

namespace Plume.Tests;

public partial class ToolCallingTests
{
    public sealed record GetWeatherArgs(string City);
    public sealed record WeatherResult(string City, int TempF, string Conditions);

    [JsonSerializable(typeof(GetWeatherArgs))]
    [JsonSerializable(typeof(WeatherResult))]
    public sealed partial class ToolContext : JsonSerializerContext { }

    private const string WeatherSchema = """
        {"type":"object","properties":{"City":{"type":"string"}},"required":["City"]}
        """;

    [Fact]
    public async Task ChatSessionAutoLoopExecutesToolAndReturnsFinalAnswer()
    {
        var p = new FakeProvider("p");
        // Round 1: model wants to call get_weather
        p.QueueToolCall("get_weather", "{\"City\":\"Boston\"}", id: "call-1");
        // Round 2: model returns the final answer using the tool result
        p.QueueResponse("It's 72°F and sunny in Boston.");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        var session = client.NewChat();
        session.UseTools(ToolBinder.Bind<GetWeatherArgs, WeatherResult>(
            "get_weather",
            "Get current weather for a city",
            WeatherSchema,
            ToolContext.Default.GetWeatherArgs,
            ToolContext.Default.WeatherResult,
            (args, ct) => Task.FromResult(new WeatherResult(args.City, 72, "sunny"))));

        var answer = await session.AskAsync("What's the weather in Boston?");

        Assert.Equal("It's 72°F and sunny in Boston.", answer);
        Assert.Equal(2, p.CallCount);

        // History: user, assistant(with tool_calls), tool, assistant(final)
        Assert.Equal(4, session.History.Count);
        Assert.Equal(MessageRole.User, session.History[0].Role);
        Assert.Equal(MessageRole.Assistant, session.History[1].Role);
        Assert.NotNull(session.History[1].ToolCalls);
        Assert.Equal(MessageRole.Tool, session.History[2].Role);
        Assert.Equal("call-1", session.History[2].ToolCallId);
        Assert.Contains("\"City\":\"Boston\"", session.History[2].Content);
        Assert.Contains("\"TempF\":72", session.History[2].Content);
        Assert.Equal(MessageRole.Assistant, session.History[3].Role);
        Assert.Equal("It's 72°F and sunny in Boston.", session.History[3].Content);
    }

    [Fact]
    public async Task ChatSessionPassesToolDefinitionsToProvider()
    {
        var p = new FakeProvider("p");
        p.QueueResponse("done");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        var session = client.NewChat();
        session.UseTools(ToolBinder.Bind<GetWeatherArgs>(
            "get_weather",
            "Get current weather",
            WeatherSchema,
            ToolContext.Default.GetWeatherArgs,
            (args, ct) => Task.FromResult($"result for {args.City}")));

        await session.AskAsync("hi");

        Assert.NotNull(p.LastRequest!.Tools);
        Assert.Single(p.LastRequest.Tools!);
        Assert.Equal("get_weather", p.LastRequest.Tools![0].Name);
        Assert.Equal("Get current weather", p.LastRequest.Tools[0].Description);
    }

    [Fact]
    public async Task ChatSessionReportsErrorWhenModelCallsUnregisteredTool()
    {
        var p = new FakeProvider("p");
        p.QueueToolCall("ghost_tool", "{}", id: "x1");
        p.QueueResponse("I tried but the tool failed.");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        var session = client.NewChat();
        // Register a different tool so the session has tools enabled at all
        session.UseTools(ToolBinder.Bind<GetWeatherArgs>(
            "get_weather", "x", WeatherSchema, ToolContext.Default.GetWeatherArgs,
            (a, ct) => Task.FromResult("ok")));

        var answer = await session.AskAsync("call the ghost");

        Assert.Equal("I tried but the tool failed.", answer);
        var toolMsg = session.History.First(m => m.Role == MessageRole.Tool);
        Assert.Contains("not registered", toolMsg.Content);
    }

    [Fact]
    public async Task ChatSessionReportsExceptionFromHandler()
    {
        var p = new FakeProvider("p");
        p.QueueToolCall("bomb", "{\"City\":\"X\"}", id: "b1");
        p.QueueResponse("recovered");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        var session = client.NewChat();
        session.UseTools(ToolBinder.Bind<GetWeatherArgs>(
            "bomb", "throws", WeatherSchema, ToolContext.Default.GetWeatherArgs,
            (args, ct) => throw new InvalidOperationException("boom!")));

        var answer = await session.AskAsync("trigger bomb");

        Assert.Equal("recovered", answer);
        var toolMsg = session.History.First(m => m.Role == MessageRole.Tool);
        Assert.Contains("boom!", toolMsg.Content);
    }

    [Fact]
    public async Task ChatSessionThrowsWhenToolLoopExceedsMaxIterations()
    {
        var p = new FakeProvider("p");
        // Always queues another tool call → infinite loop
        for (int i = 0; i < 20; i++)
            p.QueueToolCall("loop", "{\"City\":\"x\"}", id: $"id-{i}");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        var session = client.NewChat();
        session.MaxToolIterations = 3;
        session.UseTools(ToolBinder.Bind<GetWeatherArgs>(
            "loop", "loops", WeatherSchema, ToolContext.Default.GetWeatherArgs,
            (a, ct) => Task.FromResult("ok")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.AskAsync("loop forever"));
    }

    [Fact]
    public void ToolBinderThrowsWhenSchemaUnavailableOnNet8()
    {
        // On net8 the auto-derive overload throws; on net9+ it succeeds.
        try
        {
            var tool = ToolBinder.Bind<GetWeatherArgs>(
                "x", "x", ToolContext.Default.GetWeatherArgs,
                (a, ct) => Task.FromResult("ok"));
#if NET9_0_OR_GREATER
            Assert.NotNull(tool);
            Assert.Contains("City", tool.Tool.ParametersJsonSchema);
#else
            Assert.Fail("Expected InvalidOperationException on net8.");
#endif
        }
        catch (InvalidOperationException)
        {
#if NET9_0_OR_GREATER
            Assert.Fail("Auto-derive should succeed on net9+.");
#endif
        }
    }

    [Fact]
    public async Task ToolBinderArgsRoundTrip()
    {
        var bound = ToolBinder.Bind<GetWeatherArgs>(
            "weather", "x", WeatherSchema, ToolContext.Default.GetWeatherArgs,
            (args, ct) => Task.FromResult($"got {args.City}"));

        var result = await bound.Handler("{\"City\":\"NYC\"}", default);
        Assert.Equal("got NYC", result);
    }

    [Fact]
    public async Task ToolBinderTypedResultIsSerialized()
    {
        var bound = ToolBinder.Bind<GetWeatherArgs, WeatherResult>(
            "weather", "x", WeatherSchema,
            ToolContext.Default.GetWeatherArgs,
            ToolContext.Default.WeatherResult,
            (args, ct) => Task.FromResult(new WeatherResult(args.City, 50, "rain")));

        var result = await bound.Handler("{\"City\":\"London\"}", default);

        var doc = JsonDocument.Parse(result);
        Assert.Equal("London", doc.RootElement.GetProperty("City").GetString());
        Assert.Equal(50, doc.RootElement.GetProperty("TempF").GetInt32());
    }
}
