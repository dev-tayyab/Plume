using Plume;
using Plume.Google;

var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
    ?? throw new InvalidOperationException("Set GOOGLE_API_KEY environment variable.");

using var http = new HttpClient();
var provider = new GoogleProvider(http, new GoogleProviderOptions { ApiKey = apiKey });

var ai = PlumeClient.CreateBuilder()
    .Use(provider)
    .WithDefaultModel("gemini-2.5-flash")
    .Build();

Console.Write("Question: What is the capital of France?\nAnswer: ");

await foreach (var chunk in ai.StreamAsync("What is the capital of France?"))
    Console.Write(chunk);

Console.WriteLine();
Console.WriteLine();

// Multi-turn chat
Console.WriteLine("--- Multi-turn chat ---");
var chat = ai.NewChat(system: "You are a concise assistant. Answer in one sentence.");

var first = await chat.AskAsync("What's 2+2?");
Console.WriteLine($"Q: 2+2?  A: {first}");

var second = await chat.AskAsync("Now multiply that by 10.");
Console.WriteLine($"Q: x 10? A: {second}");