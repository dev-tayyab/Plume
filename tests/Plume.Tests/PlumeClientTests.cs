using Microsoft.Extensions.DependencyInjection;
using Plume.DependencyInjection;
using Plume.OpenAI;
using Plume.Anthropic;
using Plume.Google;
using Xunit;

namespace Plume.Tests;

public class PlumeClientTests
{
    [Fact]
    public async Task AskAsyncReturnsProviderContent()
    {
        var p = new FakeProvider("p");
        p.QueueResponse("answer");
        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        Assert.Equal("answer", await client.AskAsync("question"));
    }

    [Fact]
    public async Task AskAsyncThrowsWhenNoModelConfigured()
    {
        var p = new FakeProvider("p");
        var client = PlumeClient.CreateBuilder().Use(p).Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.AskAsync("question"));
    }

    [Fact]
    public async Task AskAsyncThrowsWhenPromptEmpty()
    {
        var p = new FakeProvider("p");
        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => client.AskAsync(""));
    }

    [Fact]
    public async Task SystemPromptFromOptionsIsPrepended()
    {
        var p = new FakeProvider("p");
        p.QueueResponse("ok");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test")
            .WithDefaultSystem("Be helpful.")
            .Use(p)
            .Build();

        await client.AskAsync("hello");

        Assert.Equal(1, p.CallCount);
    }

    [Fact]
    public void BuilderThrowsWithoutProvider()
    {
        Assert.Throws<InvalidOperationException>(
            () => PlumeClient.CreateBuilder().Build());
    }

    [Fact]
    public async Task SendAsyncReturnsFullResponseWithProviderName()
    {
        var p = new FakeProvider("p");
        p.QueueResponse("answer");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        var resp = await client.SendAsync(new PlumeRequest
        {
            Messages = new[] { new Message(MessageRole.User, "hi") }
        });

        Assert.Equal("answer", resp.Content);
        Assert.Equal("p", resp.Provider);
    }

    [Fact]
    public void DiExtensionRegistersClient()
    {
        var services = new ServiceCollection();

        services.AddPlume(opts =>
        {
            opts.UseOpenAi("sk-test"); // doesn't make a real call
            opts.DefaultModel = "gpt-4o-mini";
        });

        var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IPlumeClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void DiExtensionWithFailoverRegistersClient()
    {
        var services = new ServiceCollection();

        services.AddPlume(opts =>
        {
            opts.UseAnthropic("sk-ant-test");
            opts.FallbackToOpenAI("sk-openai-test");
            opts.FallbackToGoogle("AIza-test");
            opts.DefaultModel = "claude-sonnet-4";
        });

        var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IPlumeClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void DiExtensionThrowsWithoutProvider()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddPlume(_ => { /* no provider */ }));
    }
}
