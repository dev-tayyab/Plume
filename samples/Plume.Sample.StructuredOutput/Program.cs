using System.Text.Json.Serialization;
using Plume;
using Plume.Abstractions;
using Plume.Anthropic;
using Plume.Google;
using Plume.Ollama;
using Plume.OpenAI;
using Plume.Sample.StructuredOutput;
using Plume.StructuredOutput;

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

(IProvider provider, string model) = ResolveProvider(http);

var ai = PlumeClient.CreateBuilder()
    .Use(provider)
    .WithDefaultModel(model)
    .Build();

Console.WriteLine($"Provider: {provider.Name}   Model: {model}\n");

// 1) Typed extraction — recipe from a free-form prompt.
const string prompt = """
    Give me a simple breakfast recipe for 2 people. Pick something easy.
    Be specific about quantities.
    """;

Console.WriteLine($"Prompt: {prompt}\n");

var recipe = await ai.AskAsync(prompt, RecipeContext.Default.Recipe);

Console.WriteLine($"Name:     {recipe.Name}");
Console.WriteLine($"Servings: {recipe.Servings}");
Console.WriteLine($"Time:     {recipe.PrepTimeMinutes} min");
Console.WriteLine($"Ingredients:");
foreach (var ing in recipe.Ingredients)
    Console.WriteLine($"  - {ing.Quantity} {ing.Unit} {ing.Name}");
Console.WriteLine($"Steps:");
for (int i = 0; i < recipe.Steps.Length; i++)
    Console.WriteLine($"  {i + 1}. {recipe.Steps[i]}");

// 2) Demonstrate parse-error path: a hand-crafted unhelpful prompt the model
// might botch — wrapped in try/catch.
Console.WriteLine("\n--- Trying a stricter extraction ---");
try
{
    var sentiment = await ai.AskAsync(
        "Classify this review: \"This product is okay, I guess. Not great, not awful.\"",
        SentimentContext.Default.Sentiment);
    Console.WriteLine($"Label:     {sentiment.Label}");
    Console.WriteLine($"Score:     {sentiment.Confidence:F2}");
    Console.WriteLine($"Reasoning: {sentiment.Reasoning}");
}
catch (PlumeStructuredOutputException ex)
{
    Console.WriteLine($"Failed to parse: {ex.Message}");
}

return;

static (IProvider, string) ResolveProvider(HttpClient http)
{
    var googleKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
        ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (!string.IsNullOrEmpty(googleKey))
        return (new GoogleProvider(http, new GoogleProviderOptions { ApiKey = googleKey }), "gemini-2.5-flash-lite");

    var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (!string.IsNullOrEmpty(openAiKey))
        return (new OpenAiProvider(http, new OpenAiProviderOptions { ApiKey = openAiKey }), "gpt-4o-mini");

    var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    if (!string.IsNullOrEmpty(anthropicKey))
        return (new AnthropicProvider(http, new AnthropicProviderOptions { ApiKey = anthropicKey }), "claude-3-5-haiku-latest");

    Console.WriteLine("No cloud key set — falling back to local Ollama.");
    Console.WriteLine("    ollama pull llama3.2\n");
    return (new OllamaProvider(http, new OllamaProviderOptions()), "llama3.2");
}
