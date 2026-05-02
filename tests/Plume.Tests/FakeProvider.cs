using System.Runtime.CompilerServices;
using Plume.Abstractions;
using Plume.Tools;

namespace Plume.Tests;

/// <summary>
/// A controllable test double for IStreamingProvider.
/// Use the constructor to set name and supported-model predicate;
/// queue responses or exceptions with QueueResponse / QueueException.
/// </summary>
internal sealed class FakeProvider(string name, Func<string, bool>? supports = null) : IStreamingProvider
{
    private readonly Queue<Func<ProviderResponse>> _responseFactories = new();
    private readonly Queue<Func<IEnumerable<ProviderStreamChunk>>> _streamFactories = new();
    private readonly Func<string, bool> _supports = supports ?? (_ => true);

    public string Name { get; } = name;
    public int CallCount { get; private set; }
    private int StreamCallCount { get; set; }

    public bool Supports(string model) => _supports(model);

    public void QueueResponse(string content, string? model = null)
    {
        _responseFactories.Enqueue(() => new ProviderResponse
        {
            Content = content,
            Model = model ?? "test-model",
            FinishReason = FinishReason.Stop
        });
    }

    public void QueueException(Exception ex) => _responseFactories.Enqueue(() => throw ex);

    public void QueueToolCall(string toolName, string argsJson, string? id = null)
    {
        _responseFactories.Enqueue(() => new ProviderResponse
        {
            Content = string.Empty,
            Model = "test-model",
            FinishReason = FinishReason.ToolCalls,
            ToolCalls = new[]
            {
                new ToolCall
                {
                    Id = id ?? $"call-{Guid.NewGuid():N}",
                    Name = toolName,
                    ArgumentsJson = argsJson
                }
            }
        });
    }

    /// <summary>The last request the provider received (useful for asserting Tools/ToolChoice).</summary>
    public ProviderRequest? LastRequest { get; private set; }

    public void QueueStream(params string[] chunks)
    {
        var captured = chunks.ToArray();
        _streamFactories.Enqueue(() => captured.Select(c => new ProviderStreamChunk { Content = c }));
    }

    public void QueueStreamException(Exception ex)
    {
        _streamFactories.Enqueue(() => throw ex);
    }

    public Task<ProviderResponse> SendAsync(ProviderRequest request, CancellationToken ct)
    {
        CallCount++;
        LastRequest = request;
        if (_responseFactories.Count == 0)
            throw new InvalidOperationException($"FakeProvider '{Name}' has no queued response.");
        return Task.FromResult(_responseFactories.Dequeue()());
    }

    public async IAsyncEnumerable<ProviderStreamChunk> StreamAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        StreamCallCount++;
        if (_streamFactories.Count == 0)
            throw new InvalidOperationException($"FakeProvider '{Name}' has no queued stream.");

        var chunks = _streamFactories.Dequeue()();

        foreach (var c in chunks)
        {
            yield return c;
            await Task.Yield();
        }
    }
}

/// <summary>
/// A non-streaming variant of FakeProvider, useful for testing the
/// "this provider doesn't stream" path in failover.
/// </summary>
internal sealed class FakeNonStreamingProvider(string name) : IProvider
{
    private readonly Queue<Func<ProviderResponse>> _responseFactories = new();
    public string Name { get; } = name;
    private int CallCount { get; set; }
    public bool Supports(string model) => true;

    public void QueueResponse(string content)
    {
        _responseFactories.Enqueue(() => new ProviderResponse
        {
            Content = content,
            Model = "test-model",
            FinishReason = FinishReason.Stop
        });
    }

    public Task<ProviderResponse> SendAsync(ProviderRequest request, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(_responseFactories.Dequeue()());
    }
}
