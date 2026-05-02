# Introducing Plume — a tiny, multi-provider AI client for .NET

If you're a .NET developer and you've tried wiring up an LLM lately, you've
probably noticed something: every option is either a giant framework you
don't want, or an SDK locked to one vendor.

`Microsoft.Extensions.AI` is the closest thing to a standard, but it's still
maturing and the failover story is up to you. Semantic Kernel is huge.
Provider SDKs (the official OpenAI, Anthropic, and Google libraries) work
fine — until you want to swap providers, or have a fallback when the primary
one is rate-limited.

I wanted something smaller. So I built **Plume**.

```bash
dotnet add package Plume
dotnet add package Plume.OpenAI
```

```csharp
using var http = new HttpClient();
var ai = PlumeClient.CreateBuilder()
    .Use(new OpenAiProvider(http, new() { ApiKey = "sk-..." }))
    .WithDefaultModel("gpt-4o-mini")
    .Build();

string answer = await ai.AskAsync("What is the capital of France?");
```

That's it. Zero ceremony, no `KernelBuilder`, no plugins.

## What's actually different

**One API, four providers.** OpenAI, Anthropic, Google Gemini, and Ollama
all behind the same interface. Same `AskAsync`, same `StreamAsync`, same
`NewChat`. Swap providers by changing one line.

**Failover is a first-class feature.** This is the part that doesn't really
exist anywhere else in .NET:

```csharp
builder.Services.AddPlume(options =>
{
    options.UseAnthropic(anthropicKey);     // primary
    options.FallbackToOpenAI(openAiKey);    // if Anthropic 429s or 503s
    options.FallbackToGoogle(googleKey);    // then Google
    options.FallbackToOllama();             // local last resort
    options.DefaultModel = "claude-sonnet-4";
});
```

If Anthropic returns 429 or 503, Plume silently moves to OpenAI. If that
fails too, Google. Then your local Ollama. Your app keeps responding. No
try/catch, no Polly policies to wire up, no code changes downstream.

**Tiny.** The core package has minimal dependencies. No Azure SDK pulled in
transitively. Provider packages are opt-in — install only the ones you use.

**Modern .NET.** `IAsyncEnumerable` streaming. `CancellationToken`
everywhere. AOT-ready with source-generated JSON. Targets net8/9/10.

## A 10-line provider swap

This is the demo I'd want to see if I were evaluating Plume.

```csharp
// Was OpenAI...
var ai = PlumeClient.CreateBuilder()
    .Use(new OpenAiProvider(http, new() { ApiKey = openAiKey }))
    .WithDefaultModel("gpt-4o-mini")
    .Build();

// Now Gemini — the rest of your app doesn't change.
var ai = PlumeClient.CreateBuilder()
    .Use(new GoogleProvider(http, new() { ApiKey = googleKey }))
    .WithDefaultModel("gemini-1.5-flash")
    .Build();

// Same calls work either way:
await ai.AskAsync("...");
await foreach (var chunk in ai.StreamAsync("...")) Console.Write(chunk);
```

## Status

Plume is at `0.1.0-alpha.1` on NuGet today. The core API (ask / stream /
chat / failover) is shipping with all four providers. Tool calling,
structured output, and embeddings just landed in the latest build.

The roadmap to 1.0:

- 0.2 — typed responses + tool calling polish
- 0.3 — MCP client
- 0.4 — vision/multimodal input
- 0.5 — RAG helpers
- 1.0 — API freeze

## Try it

- NuGet: <https://www.nuget.org/packages/Plume>
- Source: <https://github.com/dev-tayyab/Plume>
- Samples: <https://github.com/dev-tayyab/Plume/tree/main/samples>

If you build something with it — or hit a rough edge — open an issue.
That's the fastest way to shape what 0.2 looks like.
