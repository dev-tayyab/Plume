using Plume;
using Plume.Anthropic;
using Plume.Google;
using Plume.Ollama;
using Plume.OpenAI;

// Failover demo: Anthropic primary -> OpenAI fallback -> Google fallback -> Ollama (local)
// If any provider returns 429/503/network error, Plume silently moves to the next.

using var anthropicHttp = new HttpClient();
using var openaiHttp = new HttpClient();
using var googleHttp = new HttpClient();
using var ollamaHttp = new HttpClient();

var builder = PlumeClient.CreateBuilder()
    .WithDefaultModel("claude-sonnet-4")
    .WithDefaultMaxTokens(256);

if (Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is { Length: > 0 } anthropicKey)
{
    builder.Use(new AnthropicProvider(anthropicHttp,
        new AnthropicProviderOptions { ApiKey = anthropicKey }));
}

if (Environment.GetEnvironmentVariable("OPENAI_API_KEY") is { Length: > 0 } openKey)
{
    builder.AddFallback(new OpenAiProvider(openaiHttp,
        new OpenAiProviderOptions { ApiKey = openKey }));
}

if (Environment.GetEnvironmentVariable("GOOGLE_API_KEY") is { Length: > 0 } googleKey)
{
    builder.AddFallback(new GoogleProvider(googleHttp,
        new GoogleProviderOptions { ApiKey = googleKey }));
}

// Ollama as local fallback (assumes ollama running on localhost)
builder.AddFallback(new OllamaProvider(ollamaHttp, new OllamaProviderOptions()));

var ai = builder.Build();

Console.WriteLine("Asking through failover chain...");
Console.Write("Answer: ");
await foreach (var chunk in ai.StreamAsync("Tell me a haiku about resilient systems."))
    Console.Write(chunk);
Console.WriteLine();
