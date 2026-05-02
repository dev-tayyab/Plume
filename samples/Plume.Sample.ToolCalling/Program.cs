using Plume;
using Plume.Abstractions;
using Plume.Anthropic;
using Plume.Google;
using Plume.Ollama;
using Plume.OpenAI;
using Plume.Sample.ToolCalling;
using Plume.Tools;

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

(IProvider provider, string model) = ResolveProvider(http);

var ai = PlumeClient.CreateBuilder()
    .Use(provider)
    .WithDefaultModel(model)
    .Build();

Console.WriteLine($"Provider: {provider.Name}   Model: {model}\n");

// Schemas are written by hand here so the sample also runs on .NET 8 (where
// JsonSchemaExporter isn't available). On .NET 9+ you can drop these and use
// the auto-deriving Bind overload.
const string WeatherSchema = """
    {"type":"object","properties":{"City":{"type":"string","description":"City name, e.g. 'Boston'"}},"required":["City"]}
    """;

const string CalculateSchema = """
    {"type":"object","properties":{"A":{"type":"number"},"B":{"type":"number"},"Operator":{"type":"string","enum":["+","-","*","/"]}},"required":["A","B","Operator"]}
    """;

const string SearchSchema = """
    {"type":"object","properties":{"Query":{"type":"string"},"MaxResults":{"type":"integer","minimum":1,"maximum":10}},"required":["Query","MaxResults"]}
    """;

var weatherTool = ToolBinder.Bind<GetWeatherArgs, WeatherResult>(
    "get_weather",
    "Get the current weather for a city.",
    WeatherSchema,
    ToolsContext.Default.GetWeatherArgs,
    ToolsContext.Default.WeatherResult,
    (args, ct) =>
    {
        Console.WriteLine($"  [tool] get_weather(City={args.City})");
        // Stub: real impl would call a weather API.
        var data = args.City.ToLowerInvariant() switch
        {
            "boston" => new WeatherResult("Boston", 4.5, "snow flurries"),
            "tokyo" => new WeatherResult("Tokyo", 18.0, "clear"),
            "paris" => new WeatherResult("Paris", 12.0, "overcast"),
            _ => new WeatherResult(args.City, 20.0, "fair")
        };
        return Task.FromResult(data);
    });

var calculatorTool = ToolBinder.Bind<CalculateArgs, CalculateResult>(
    "calculate",
    "Perform a basic arithmetic operation.",
    CalculateSchema,
    ToolsContext.Default.CalculateArgs,
    ToolsContext.Default.CalculateResult,
    (args, ct) =>
    {
        Console.WriteLine($"  [tool] calculate({args.A} {args.Operator} {args.B})");
        var result = args.Operator switch
        {
            "+" => args.A + args.B,
            "-" => args.A - args.B,
            "*" => args.A * args.B,
            "/" => args.B == 0 ? double.NaN : args.A / args.B,
            _ => double.NaN
        };
        return Task.FromResult(new CalculateResult(result, $"{args.A} {args.Operator} {args.B} = {result}"));
    });

// Tiny in-memory knowledge base for the search tool.
var knowledgeBase = new (string Title, string Snippet)[]
{
    ("Pancakes 101", "Basic pancakes need flour, eggs, milk, and a pinch of salt. Cook on medium heat."),
    ("French Crepes", "Crepes are thinner than pancakes and made with the same base plus more milk."),
    ("Sourdough Pancakes", "Use sourdough starter for tangy pancakes; let the batter rest 30 minutes."),
    ("Waffles", "Waffles use a similar batter to pancakes but with more fat for crispness."),
};

var searchTool = ToolBinder.Bind<SearchKnowledgeArgs, SearchResults>(
    "search_knowledge",
    "Search a small in-memory knowledge base.",
    SearchSchema,
    ToolsContext.Default.SearchKnowledgeArgs,
    ToolsContext.Default.SearchResults,
    (args, ct) =>
    {
        Console.WriteLine($"  [tool] search_knowledge(Query={args.Query}, MaxResults={args.MaxResults})");
        var hits = knowledgeBase
            .Where(kv => kv.Snippet.Contains(args.Query, StringComparison.OrdinalIgnoreCase)
                      || kv.Title.Contains(args.Query, StringComparison.OrdinalIgnoreCase))
            .Take(args.MaxResults)
            .Select(kv => new SearchHit(kv.Title, kv.Snippet))
            .ToArray();
        return Task.FromResult(new SearchResults(hits));
    });

var session = ai.NewChat(system:
    "You are a helpful assistant. Use the tools when they're relevant. " +
    "Be concise — one or two sentences per answer.");
session.UseTools(weatherTool, calculatorTool, searchTool);

string[] questions =
[
    "What's the weather in Boston right now, in Fahrenheit?",
    "What's 47 multiplied by 113?",
    "Find me info on pancakes."
];

foreach (var q in questions)
{
    Console.WriteLine($"\n> {q}");
    var answer = await session.AskAsync(q);
    Console.WriteLine($"  {answer}");
}

return;

static (IProvider, string) ResolveProvider(HttpClient http)
{
    var googleKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
        ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (!string.IsNullOrEmpty(googleKey))
        return (new GoogleProvider(http, new GoogleProviderOptions { ApiKey = googleKey }), "gemini-2.5-flash");

    var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (!string.IsNullOrEmpty(openAiKey))
        return (new OpenAiProvider(http, new OpenAiProviderOptions { ApiKey = openAiKey }), "gpt-4o-mini");

    var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    if (!string.IsNullOrEmpty(anthropicKey))
        return (new AnthropicProvider(http, new AnthropicProviderOptions { ApiKey = anthropicKey }), "claude-3-5-haiku-latest");

    Console.WriteLine("No cloud key set — falling back to local Ollama.");
    Console.WriteLine("    ollama pull llama3.2  (must be a model that supports tool calls)\n");
    return (new OllamaProvider(http, new OllamaProviderOptions()), "llama3.2");
}
