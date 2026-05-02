# Plume 🪶

**The minimalist, resilient AI client for .NET.**
One unified API. Multiple providers. Built-in failover.

[![NuGet](https://img.shields.io/nuget/v/Plume.svg?label=Plume)](https://www.nuget.org/packages/Plume)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Plume.svg)](https://www.nuget.org/packages/Plume)
[![CI](https://github.com/dev-tayyab/Plume/actions/workflows/ci.yml/badge.svg)](https://github.com/dev-tayyab/Plume/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/dev-tayyab/Plume/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4)](https://dotnet.microsoft.com/)

| Package | Version |
| --- | --- |
| [Plume](https://www.nuget.org/packages/Plume) | [![NuGet](https://img.shields.io/nuget/v/Plume.svg)](https://www.nuget.org/packages/Plume) |
| [Plume.OpenAI](https://www.nuget.org/packages/Plume.OpenAI) | [![NuGet](https://img.shields.io/nuget/v/Plume.OpenAI.svg)](https://www.nuget.org/packages/Plume.OpenAI) |
| [Plume.Anthropic](https://www.nuget.org/packages/Plume.Anthropic) | [![NuGet](https://img.shields.io/nuget/v/Plume.Anthropic.svg)](https://www.nuget.org/packages/Plume.Anthropic) |
| [Plume.Google](https://www.nuget.org/packages/Plume.Google) | [![NuGet](https://img.shields.io/nuget/v/Plume.Google.svg)](https://www.nuget.org/packages/Plume.Google) |
| [Plume.Ollama](https://www.nuget.org/packages/Plume.Ollama) | [![NuGet](https://img.shields.io/nuget/v/Plume.Ollama.svg)](https://www.nuget.org/packages/Plume.Ollama) |

```csharp
using var http = new HttpClient();
var ai = PlumeClient.CreateBuilder()
    .Use(new OpenAIProvider(http, new() { ApiKey = "sk-..." }))
    .WithDefaultModel("gpt-4o-mini")
    .Build();

string answer = await ai.AskAsync("What is the capital of France?");
```

That's it. From zero to a working LLM call in two lines. No `KernelBuilder`, no plugins, no ceremony.

---

## Why Plume?

The .NET AI ecosystem is dominated by heavyweight frameworks designed for enterprise. Plume is for the rest of us:

- **Tiny.** Core package has minimal dependencies. No Azure SDK pulled in transitively.
- **Multi-provider.** OpenAI, Anthropic Claude, Google Gemini, Ollama — same API, swap at will.
- **Resilient by default.** Provider down? Plume fails over silently to the next one. Your app keeps responding.
- **Async-first.** `IAsyncEnumerable` streaming. `CancellationToken` everywhere. Modern .NET 8+.
- **AOT-ready.** Source-generated JSON, trim-safe, Native AOT compatible.

## Install

```bash
dotnet add package Plume
dotnet add package Plume.OpenAI       # optional
dotnet add package Plume.Anthropic    # optional
dotnet add package Plume.Google       # optional
dotnet add package Plume.Ollama       # optional
```

Install only the providers you use. Plume itself does not depend on any specific provider.

## Quick start (DI / ASP.NET)

```csharp
using Plume.DependencyInjection;
using Plume.OpenAI;

builder.Services.AddPlume(options =>
{
    options.UseOpenAI(builder.Configuration["OpenAI:ApiKey"]!);
    options.DefaultModel = "gpt-4o-mini";
});

// Anywhere in your app:
public class SummaryService(IPlumeClient ai)
{
    public Task<string> Summarize(string text) =>
        ai.AskAsync($"Summarize in 2 lines: {text}");
}
```

## Streaming

```csharp
await foreach (var chunk in ai.StreamAsync("Write a haiku about rain"))
    Console.Write(chunk);
```

## Multi-turn conversations

```csharp
var chat = ai.NewChat(system: "You are a senior C# code reviewer.");

await chat.AskAsync("Review: public string Hi() => \"hi\";");
await chat.AskAsync("How can I make it more idiomatic?");

// Persist or restore
foreach (var message in chat.History)
    SaveToDatabase(message);
```

## Failover — the killer feature

```csharp
using Plume.Anthropic;
using Plume.OpenAI;
using Plume.Google;
using Plume.Ollama;

builder.Services.AddPlume(options =>
{
    options.UseAnthropic(anthropicKey);          // primary
    options.FallbackToOpenAI(openAiKey);         // if Anthropic fails transiently
    options.FallbackToGoogle(googleKey);         // then Google
    options.FallbackToOllama();                  // local last resort

    options.DefaultModel = "claude-sonnet-4";
});
```

If Anthropic returns 429 or 503, Plume silently fails over to OpenAI, then Google, then your local Ollama. Your application keeps responding. No code changes, no try/catch. **This is the gap nothing else in the .NET ecosystem fills.**

What counts as transient (failover happens):
- HTTP 429 (rate limit) — uses `Retry-After` if provided
- HTTP 503/504/502/408 (server unavailable, gateway timeout)
- `HttpRequestException` (network failure)
- Timeouts

What does NOT count as transient (failover does NOT happen):
- HTTP 4xx other than 429 (bad request, auth failure, not found)
- Cancellation requested by your `CancellationToken`

## Provider-specific options (strongly typed)

```csharp
var answer = await ai.AskAsync("Generate a story",
    new AskOptions
    {
        Temperature = 0.9,
        Extensions = new OpenAIExtensions
        {
            FrequencyPenalty = 0.5,
            Seed = 42
        }
    });
```

IntelliSense works. Compile-time safety. No stringly-typed dictionaries.
Provider-specific extensions are silently ignored if the request fails over to a different provider — so you can use them safely with failover enabled.

## Supported providers

| Provider | Package | Model prefix | Streaming |
| -------- | ------- | ------------ | :-------: |
| OpenAI (and Azure / OpenRouter / compatible) | `Plume.OpenAI` | `gpt-*`, `o1*`, `o3*`, `o4*` | ✅ |
| Anthropic Claude | `Plume.Anthropic` | `claude-*` | ✅ |
| Google Gemini | `Plume.Google` | `gemini*` | ✅ |
| Ollama (local models) | `Plume.Ollama` | any | ✅ |

## Samples

Runnable end-to-end samples live in [`samples/`](https://github.com/dev-tayyab/Plume/tree/main/samples). Each is a single-file program — clone the repo, set the relevant `*_API_KEY` env var, and `dotnet run`.

| Sample | What it shows |
| --- | --- |
| [Console](https://github.com/dev-tayyab/Plume/tree/main/samples/Plume.Sample.Console) | Streaming + multi-turn chat against OpenAI in ~20 lines |
| [Console.Google](https://github.com/dev-tayyab/Plume/tree/main/samples/Plume.Sample.Console.Google) | Same flow against Google Gemini — drop-in provider swap |
| [Failover](https://github.com/dev-tayyab/Plume/tree/main/samples/Plume.Sample.Failover) | Anthropic → OpenAI → Google → Ollama failover chain |
| [ToolCalling](https://github.com/dev-tayyab/Plume/tree/main/samples/Plume.Sample.ToolCalling) | Provider-agnostic function/tool calling |
| [StructuredOutput](https://github.com/dev-tayyab/Plume/tree/main/samples/Plume.Sample.StructuredOutput) | Source-generated typed responses (JSON schema, no reflection) |
| [Embeddings](https://github.com/dev-tayyab/Plume/tree/main/samples/Plume.Sample.Embeddings) | Embedding vectors with the same provider abstraction |

## Status

🚧 **v0.1.0-alpha** — under active development, API may evolve before 1.0.

The roadmap:

| Version | Focus |
| :-----: | --- |
| 0.1 | Core API: ask, stream, chat, failover. 4 providers shipping. |
| 0.2 | Source-generated typed responses. Tool calling. |
| 0.3 | MCP client support. Embeddings. |
| 0.4 | Vision/multimodal input. |
| 0.5 | Retrieval helpers (RAG). |
| 1.0 | API freeze. Production-ready. |

## Contributing

Issues and PRs welcome.

## License

MIT. Use it freely in commercial projects.

---

Built with ❤️ for .NET developers who want their AI code to read like it was written, not generated.
