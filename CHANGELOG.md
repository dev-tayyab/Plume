# Changelog

All notable changes to Plume will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
