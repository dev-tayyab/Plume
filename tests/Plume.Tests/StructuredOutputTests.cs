using System.Text.Json;
using System.Text.Json.Serialization;
using Plume.Abstractions;
using Plume.StructuredOutput;
using Xunit;

namespace Plume.Tests;

public partial class StructuredOutputTests
{
    public sealed record Recipe(string Name, int Servings, string[] Ingredients);

    [JsonSerializable(typeof(Recipe))]
    public sealed partial class RecipeContext : JsonSerializerContext { }

#if NET9_0_OR_GREATER
    [Fact]
    public async Task AskAsyncTReturnsTypedValue()
    {
        var p = new RecordingFakeProvider();
        p.QueueResponse(@"{""Name"":""Pancakes"",""Servings"":4,""Ingredients"":[""flour"",""milk"",""eggs""]}");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        var recipe = await client.AskAsync("Give me a pancake recipe", RecipeContext.Default.Recipe);

        Assert.Equal("Pancakes", recipe.Name);
        Assert.Equal(4, recipe.Servings);
        Assert.Equal(3, recipe.Ingredients.Length);
    }

    [Fact]
    public async Task AskAsyncTPropagatesResponseSchemaToProvider()
    {
        var p = new RecordingFakeProvider();
        p.QueueResponse(@"{""Name"":""x"",""Servings"":1,""Ingredients"":[]}");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        await client.AskAsync("hi", RecipeContext.Default.Recipe);

        var captured = p.LastRequest;
        Assert.NotNull(captured);
        Assert.NotNull(captured!.ResponseSchema);
        Assert.Equal("Recipe", captured.ResponseSchema!.Name);
        Assert.True(captured.ResponseSchema.Strict);
    }

    [Fact]
    public async Task AskAsyncTThrowsOnNet8WhenSchemaUnavailable()
    {
        // No-op on net9+ where schema generation works; the matching net8 test below
        // covers the throw path.
        await Task.CompletedTask;
    }
#else
    [Fact]
    public async Task AskAsyncTThrowsOnNet8WhenSchemaUnavailable()
    {
        var p = new RecordingFakeProvider();
        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        await Assert.ThrowsAsync<PlumeStructuredOutputException>(
            () => client.AskAsync("hi", RecipeContext.Default.Recipe));
    }
#endif

    [Fact]
    public async Task AskAsyncTHonorsCallerSuppliedSchemaFromAskOptions()
    {
        var p = new RecordingFakeProvider();
        p.QueueResponse(@"{""Name"":""x"",""Servings"":1,""Ingredients"":[]}");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        var explicitSchema = """{"type":"object","properties":{"foo":{"type":"string"}}}""";
        var options = new AskOptions
        {
            ResponseSchema = new ResponseSchemaSpec
            {
                SchemaJson = explicitSchema,
                Name = "MyOverride",
                Strict = false
            }
        };

        await client.AskAsync("hi", RecipeContext.Default.Recipe, options);

        var captured = p.LastRequest!;
        Assert.Equal(explicitSchema, captured.ResponseSchema!.SchemaJson);
        Assert.Equal("MyOverride", captured.ResponseSchema.Name);
        Assert.False(captured.ResponseSchema.Strict);
    }

    [Fact]
    public async Task AskAsyncTThrowsPlumeStructuredOutputExceptionOnMalformedJson()
    {
        var p = new RecordingFakeProvider();
        p.QueueResponse("this is not json");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        await Assert.ThrowsAsync<PlumeStructuredOutputException>(
            () => client.AskAsync("hi", RecipeContext.Default.Recipe));
    }

    [Fact]
    public async Task AskAsyncTThrowsOnEmptyContent()
    {
        var p = new RecordingFakeProvider();
        p.QueueResponse("");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        await Assert.ThrowsAsync<PlumeStructuredOutputException>(
            () => client.AskAsync("hi", RecipeContext.Default.Recipe));
    }

    [Fact]
    public async Task AskAsyncTThrowsOnJsonNullLiteral()
    {
        var p = new RecordingFakeProvider();
        p.QueueResponse("null");

        var client = PlumeClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        await Assert.ThrowsAsync<PlumeStructuredOutputException>(
            () => client.AskAsync("hi", RecipeContext.Default.Recipe));
    }

    [Fact]
    public void SchemaGeneratorProducesNonEmptySchemaOnNet9Plus()
    {
        var schema = JsonSchemaGenerator.TryGenerate(RecipeContext.Default.Recipe);

#if NET9_0_OR_GREATER
        Assert.NotNull(schema);
        Assert.Contains("properties", schema!);
        Assert.Contains("Name", schema);
        Assert.Contains("Servings", schema);
        Assert.Contains("Ingredients", schema);
#else
        Assert.Null(schema);
#endif
    }

    [Fact]
    public void SchemaGeneratorThrowsOnNullTypeInfo()
    {
        Assert.Throws<ArgumentNullException>(() =>
            JsonSchemaGenerator.TryGenerate(null!));
    }

    /// <summary>Fake provider that records the last ProviderRequest it received.</summary>
    internal sealed class RecordingFakeProvider : IProvider
    {
        private readonly Queue<Func<ProviderResponse>> _factories = new();
        public ProviderRequest? LastRequest { get; private set; }
        public string Name => "recorder";
        public bool Supports(string model) => true;

        public void QueueResponse(string content) =>
            _factories.Enqueue(() => new ProviderResponse
            {
                Content = content,
                Model = "test",
                FinishReason = FinishReason.Stop
            });

        public Task<ProviderResponse> SendAsync(ProviderRequest request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(_factories.Dequeue()());
        }
    }
}
