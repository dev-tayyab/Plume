# Changelog

All notable changes to Plume will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Tool calling** — first-class function/tool calling across all chat providers:
  - `Plume.Tools.Tool`, `ToolCall`, `ToolChoice` (Auto / Required / None / Specific)
  - `BoundTool` + `ToolBinder.Bind<TArgs[, TResult]>(...)` — strongly-typed handlers, AOT-safe via `JsonTypeInfo`
  - `Message` gained `ToolCalls` and `ToolCallId` for round-tripping tool turns
  - `IChatSession.UseTools(...)` — auto-loop that executes registered handlers and replays the request until the model stops calling tools (`MaxToolIterations`, default 10)
  - Wire-formats: OpenAI `tools` / `tool_calls`, Anthropic `tool_use` / `tool_result` blocks, Gemini `functionDeclarations` / `functionCall`, Ollama `tools`
- **Structured output** — typed `IPlumeClient.AskAsync<T>(prompt, JsonTypeInfo<T>, ...)`:
  - Auto-derives JSON Schema from `JsonTypeInfo` on .NET 9+ (`System.Text.Json.Schema.JsonSchemaExporter`); explicit-schema overload for .NET 8 and hand-tuned schemas
  - Provider-side enforcement: OpenAI `response_format=json_schema`, Gemini `responseSchema`, Ollama `format`, Anthropic via system-prompt injection (will upgrade to tool-call recipe later)
  - `PlumeStructuredOutputException` wraps malformed-JSON failures
- **Embeddings** — first-class support for text embeddings:
  - `IEmbeddingClient` / `IEmbeddingProvider` seam mirroring chat
  - `Embedding` value type with `CosineSimilarity` (instance + static span overload)
  - Failover-aware `EmbeddingClient.CreateBuilder()` and `services.AddPlumeEmbeddings(...)`
  - Providers: `Plume.OpenAI` (`/v1/embeddings`), `Plume.Google` (`batchEmbedContents`), `Plume.Ollama` (`/api/embed`)
  - Anthropic intentionally omitted — no first-party embedding endpoint
- Multi-targeting: all packages now ship for `net8.0`, `net9.0`, and `net10.0`.
- Package icon (`assets/icon.png`) and per-package `PackageTags` / `Description`.
- Public API surface tracking via `Microsoft.CodeAnalysis.PublicApiAnalyzers` (`PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` per project).
- Reproducible builds: `ContinuousIntegrationBuild` + `Deterministic` enabled in CI.
- NuGet badge table in README covering all five packages.

### Changed

- Versioning is now driven by [MinVer](https://github.com/adamralph/minver) from git tags (prefix `v`); the release workflow no longer passes `-p:Version`.
- CI and release workflows install the .NET 8, 9, and 10 SDKs.

## [0.1.0-alpha.1] — 2026-05-01

### Added

- Core `Plume` package
  - `IPlumeClient` with `AskAsync`, `StreamAsync`, `NewChat`, `SendAsync`
  - `IProvider` and `IStreamingProvider` provider seam
  - `FailoverProvider` decorator for transparent multi-provider failover
  - `IChatSession` for multi-turn conversations with history
  - Strongly-typed provider extensions via `IProviderExtensions`
  - `services.AddPlume(...)` DI integration
  - `PlumeClient.CreateBuilder()` fluent factory
  - Custom exception hierarchy (`PlumeException` and friends)
  - Built-in SSE event reader
  - Source-generated JSON for AOT compatibility
- `Plume.OpenAI` — full OpenAI / Azure OpenAI / OpenRouter / compatible support
- `Plume.Anthropic` — full Anthropic Claude Messages API support
- `Plume.Google` — full Google Gemini support
- `Plume.Ollama` — full local Ollama support (NDJSON streaming)
- Test project with failover, chat session, SSE parser, and provider tests
- Console and Failover sample apps
- GitHub Actions CI workflow (Linux / Windows / macOS, .NET 8)
- Tag-driven release workflow that publishes all five packages to NuGet

[0.1.0-alpha.1]: https://github.com/dev-tayyab/Plume/releases/tag/v0.1.0-alpha.1
