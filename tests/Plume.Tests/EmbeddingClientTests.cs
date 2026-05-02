using Microsoft.Extensions.DependencyInjection;
using Plume.Abstractions;
using Plume.DependencyInjection;
using Xunit;

namespace Plume.Tests;

public class EmbeddingClientTests
{
    [Fact]
    public async Task EmbedAsyncReturnsVectorsInOrder()
    {
        var p = new FakeEmbeddingProvider("p");
        p.QueueResponse(new[] { 1f, 2f, 3f }, new[] { 4f, 5f, 6f });

        var client = EmbeddingClient.CreateBuilder()
            .WithDefaultModel("test-embed").Use(p).Build();

        var result = await client.EmbedAsync(new[] { "hello", "world" });

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 1f, 2f, 3f }, result[0].Vector.ToArray());
        Assert.Equal(new[] { 4f, 5f, 6f }, result[1].Vector.ToArray());
    }

    [Fact]
    public async Task EmbedAsyncSingleStringExtensionWorks()
    {
        var p = new FakeEmbeddingProvider("p");
        p.QueueResponse(new[] { 0.1f, 0.2f });

        var client = EmbeddingClient.CreateBuilder()
            .WithDefaultModel("test-embed").Use(p).Build();

        var v = await client.EmbedAsync("hello");

        Assert.Equal(2, v.Dimensions);
        Assert.Equal(0.1f, v.Vector.Span[0]);
    }

    [Fact]
    public async Task EmbedAsyncThrowsWhenNoModelConfigured()
    {
        var p = new FakeEmbeddingProvider("p");
        var client = EmbeddingClient.CreateBuilder().Use(p).Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.EmbedAsync(new[] { "x" }));
    }

    [Fact]
    public async Task EmbedAsyncThrowsWhenInputsEmpty()
    {
        var p = new FakeEmbeddingProvider("p");
        var client = EmbeddingClient.CreateBuilder()
            .WithDefaultModel("test").Use(p).Build();

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.EmbedAsync(Array.Empty<string>()));
    }

    [Fact]
    public async Task FailoverFallsBackOnTransientError()
    {
        var primary = new FakeEmbeddingProvider("primary");
        var secondary = new FakeEmbeddingProvider("secondary");

        primary.QueueException(new ProviderTransientException("primary", "boom"));
        secondary.QueueResponse(new[] { 9f, 9f });

        var client = EmbeddingClient.CreateBuilder()
            .WithDefaultModel("test").Use(primary).AddFallback(secondary).Build();

        var result = await client.EmbedAsync(new[] { "x" });

        Assert.Single(result);
        Assert.Equal(9f, result[0].Vector.Span[0]);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, secondary.CallCount);
    }

    [Fact]
    public async Task FailoverSkipsProvidersThatDontSupportModel()
    {
        var primary = new FakeEmbeddingProvider("primary", supports: m => m == "other");
        var secondary = new FakeEmbeddingProvider("secondary");

        secondary.QueueResponse(new[] { 1f });

        var client = EmbeddingClient.CreateBuilder()
            .WithDefaultModel("real-model").Use(primary).AddFallback(secondary).Build();

        var result = await client.EmbedAsync(new[] { "x" });

        Assert.Single(result);
        Assert.Equal(0, primary.CallCount);
        Assert.Equal(1, secondary.CallCount);
    }

    [Fact]
    public async Task FailoverThrowsAllProvidersFailedWhenAllTransient()
    {
        var p1 = new FakeEmbeddingProvider("p1");
        var p2 = new FakeEmbeddingProvider("p2");
        p1.QueueException(new ProviderTransientException("p1", "down"));
        p2.QueueException(new ProviderTransientException("p2", "down"));

        var client = EmbeddingClient.CreateBuilder()
            .WithDefaultModel("test").Use(p1).AddFallback(p2).Build();

        await Assert.ThrowsAsync<AllProvidersFailedException>(
            () => client.EmbedAsync(new[] { "x" }));
    }

    [Fact]
    public async Task FailoverDoesNotCatchNonTransientExceptions()
    {
        var p1 = new FakeEmbeddingProvider("p1");
        var p2 = new FakeEmbeddingProvider("p2");
        p1.QueueException(new ProviderRequestException("p1", "bad request", 400));

        var client = EmbeddingClient.CreateBuilder()
            .WithDefaultModel("test").Use(p1).AddFallback(p2).Build();

        await Assert.ThrowsAsync<ProviderRequestException>(
            () => client.EmbedAsync(new[] { "x" }));
        Assert.Equal(0, p2.CallCount);
    }

    [Fact]
    public void CosineSimilarityOfIdenticalVectorsIsOne()
    {
        var a = new Embedding(new[] { 1f, 2f, 3f });
        Assert.Equal(1f, a.CosineSimilarity(a), precision: 5);
    }

    [Fact]
    public void CosineSimilarityOfOrthogonalVectorsIsZero()
    {
        var a = new Embedding(new[] { 1f, 0f });
        var b = new Embedding(new[] { 0f, 1f });
        Assert.Equal(0f, a.CosineSimilarity(b), precision: 5);
    }

    [Fact]
    public void CosineSimilarityRejectsMismatchedDimensions()
    {
        var a = new Embedding(new[] { 1f, 2f });
        var b = new Embedding(new[] { 1f, 2f, 3f });
        Assert.Throws<ArgumentException>(() => a.CosineSimilarity(b));
    }

    [Fact]
    public async Task DependencyInjectionRegistersClient()
    {
        var services = new ServiceCollection();
        services.AddPlumeEmbeddings(o =>
        {
            o.DefaultModel = "test";
            o.Use(_ =>
            {
                var p = new FakeEmbeddingProvider("p");
                p.QueueDeterministic();
                return p;
            });
        });

        var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IEmbeddingClient>();

        var v = await client.EmbedAsync("abc");
        Assert.Equal(4, v.Dimensions);
    }
}
