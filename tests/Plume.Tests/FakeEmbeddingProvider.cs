using Plume.Abstractions;

namespace Plume.Tests;

/// <summary>
/// A controllable test double for <see cref="IEmbeddingProvider"/>.
/// Queue responses or exceptions for sequential calls.
/// </summary>
internal sealed class FakeEmbeddingProvider(string name, Func<string, bool>? supports = null) : IEmbeddingProvider
{
    private readonly Queue<Func<EmbeddingRequest, EmbeddingResponse>> _factories = new();
    private readonly Func<string, bool> _supports = supports ?? (_ => true);

    public string Name { get; } = name;
    public int CallCount { get; private set; }
    public bool Supports(string model) => _supports(model);

    public void QueueResponse(params float[][] vectors)
    {
        var captured = vectors.Select(v => v.ToArray()).ToArray();
        _factories.Enqueue(req => new EmbeddingResponse
        {
            Embeddings = captured.Select(v => new Embedding(v)).ToList(),
            Model = req.Model
        });
    }

    public void QueueDeterministic()
    {
        // For each input, emit a 4-d vector hashed from the input string.
        // Stable across runs so failover/order tests can assert exact values.
        _factories.Enqueue(req =>
        {
            var vectors = req.Inputs.Select(s =>
            {
                var v = new float[4];
                for (int i = 0; i < s.Length; i++) v[i % 4] += s[i];
                return new Embedding(v);
            }).ToList();
            return new EmbeddingResponse
            {
                Embeddings = vectors,
                Model = req.Model,
                Usage = new TokenUsage(req.Inputs.Sum(s => s.Length), 0)
            };
        });
    }

    public void QueueException(Exception ex) => _factories.Enqueue(_ => throw ex);

    public Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken ct)
    {
        CallCount++;
        if (_factories.Count == 0)
            throw new InvalidOperationException($"FakeEmbeddingProvider '{Name}' has no queued response.");
        return Task.FromResult(_factories.Dequeue()(request));
    }
}
