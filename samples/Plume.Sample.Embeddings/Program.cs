using System.Globalization;
using Plume;
using Plume.Abstractions;
using Plume.Google;
using Plume.Ollama;
using Plume.OpenAI;

// Pick whichever provider has credentials in env. Falls through to Ollama (local).
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

(IEmbeddingProvider provider, string model) = ResolveProvider(http);

var ai = EmbeddingClient.CreateBuilder()
    .Use(provider)
    .WithDefaultModel(model)
    .Build();

Console.WriteLine($"Provider: {provider.Name}   Model: {model}\n");

// 1) Embed a single string.
var single = await ai.EmbedAsync("How do I make pancakes?");
var preview = single.Vector.Span[..Math.Min(4, single.Dimensions)].ToArray()
    .Select(f => f.ToString("F4", CultureInfo.InvariantCulture));
Console.WriteLine($"Single embedding: {single.Dimensions} dims, first 4 values: " +
    $"[{string.Join(", ", preview)}, ...]");

// 2) Batch embeddings.
var corpus = new[]
{
    "The cat sat on the mat.",
    "Dogs are loyal companions.",
    "Pancakes need flour, eggs, and milk.",
    "The Eiffel Tower is in Paris."
};

Console.WriteLine($"\nEmbedding {corpus.Length} documents in one batch...");
var docs = await ai.EmbedAsync(corpus);
Console.WriteLine($"Got {docs.Count} vectors, each {docs[0].Dimensions} dims.");

// 3) Cosine-similarity search.
var query = await ai.EmbedAsync("What goes into making pancakes?");

Console.WriteLine($"\nQuery: \"What goes into making pancakes?\"");
Console.WriteLine($"Ranked results by cosine similarity:\n");

var ranked = corpus
    .Select((text, i) => (text, score: query.CosineSimilarity(docs[i])))
    .OrderByDescending(x => x.score)
    .ToList();

foreach (var (text, score) in ranked)
    Console.WriteLine($"  {score:F4}  {text}");

return;

static (IEmbeddingProvider, string) ResolveProvider(HttpClient http)
{
    var googleKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
        ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (!string.IsNullOrEmpty(googleKey))
    {
        return (
            new GoogleEmbeddingProvider(http, new GoogleProviderOptions { ApiKey = googleKey }),
            "gemini-embedding-001");
    }

    var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (!string.IsNullOrEmpty(openAiKey))
    {
        return (
            new OpenAiEmbeddingProvider(http, new OpenAiProviderOptions { ApiKey = openAiKey }),
            "text-embedding-3-small");
    }

    Console.WriteLine("No GOOGLE_API_KEY or OPENAI_API_KEY set — falling back to local Ollama.");
    Console.WriteLine("Make sure `ollama serve` is running and you have pulled an embedding model:");
    Console.WriteLine("    ollama pull nomic-embed-text\n");

    return (
        new OllamaEmbeddingProvider(http, new OllamaProviderOptions()),
        "nomic-embed-text");
}
