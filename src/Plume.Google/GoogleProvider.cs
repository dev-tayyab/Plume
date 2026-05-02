using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Plume.Abstractions;
using Plume.Google.Internal;
using Plume.Tools;

namespace Plume.Google;

/// <summary>
/// Google Gemini provider. Uses the Generative Language API with SSE streaming.
/// </summary>
public sealed class GoogleProvider : IStreamingProvider
{
    private readonly HttpClient _http;
    private readonly GoogleProviderOptions _options;

    /// <summary>Create a new Google provider.</summary>
    public GoogleProvider(HttpClient http, GoogleProviderOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new ArgumentException("ApiKey is required.", nameof(options));

        _http.BaseAddress ??= new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        _http.DefaultRequestHeaders.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
    }

    /// <inheritdoc />
    public string Name => "google";

    /// <inheritdoc />
    public bool Supports(string model) =>
        !string.IsNullOrEmpty(model)
        && model.StartsWith("gemini", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<ProviderResponse> SendAsync(
        ProviderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = MapToGoogle(request);
        var url = $"{_options.ApiVersion}/models/{Uri.EscapeDataString(request.Model)}:generateContent";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, GoogleJsonContext.Default.GoogleGenerateRequest)
        };

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        var raw = await response.Content
            .ReadFromJsonAsync(GoogleJsonContext.Default.GoogleGenerateResponse, ct)
            .ConfigureAwait(false);

        if (raw is null || raw.Candidates is null || raw.Candidates.Count == 0)
            throw new ProviderRequestException(Name, "Empty response from Google.");

        var candidate = raw.Candidates[0];
        var (contentText, toolCalls) = ExtractParts(candidate.Content?.Parts);

        return new ProviderResponse
        {
            Content = contentText,
            Model = raw.ModelVersion ?? request.Model,
            FinishReason = toolCalls is { Count: > 0 } ? FinishReason.ToolCalls : MapFinishReason(candidate.FinishReason),
            ToolCalls = toolCalls,
            Usage = raw.UsageMetadata is { } u
                ? new TokenUsage(u.PromptTokenCount, u.CandidatesTokenCount)
                : null
        };
    }

    private static (string Text, List<ToolCall>? ToolCalls) ExtractParts(List<GooglePart>? parts)
    {
        if (parts is null) return (string.Empty, null);
        var sb = new System.Text.StringBuilder();
        List<ToolCall>? calls = null;
        foreach (var p in parts)
        {
            if (p.Text is { Length: > 0 } t) sb.Append(t);
            if (p.FunctionCall is { } fc)
            {
                calls ??= new();
                calls.Add(new ToolCall
                {
                    // Gemini doesn't supply IDs — synthesize a stable one from name + index.
                    Id = $"{fc.Name}-{calls.Count}",
                    Name = fc.Name,
                    ArgumentsJson = fc.Args?.GetRawText() ?? "{}"
                });
            }
        }
        return (sb.ToString(), calls);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ProviderStreamChunk> StreamAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = MapToGoogle(request);
        var url = $"{_options.ApiVersion}/models/{Uri.EscapeDataString(request.Model)}:streamGenerateContent?alt=sse";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, GoogleJsonContext.Default.GoogleGenerateRequest)
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        await HttpProviderHelpers.ThrowForNonSuccessAsync(Name, response, ct).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        await foreach (var sse in SseEventReader.ReadAsync(stream, ct).ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(sse.Data)) continue;

            GoogleGenerateResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize(
                    sse.Data, GoogleJsonContext.Default.GoogleGenerateResponse);
            }
            catch (JsonException) { continue; }

            if (chunk is null) continue;

            var candidate = chunk.Candidates?.FirstOrDefault();
            var text = candidate?.Content?.Parts is null
                ? string.Empty
                : string.Concat(candidate.Content.Parts.Select(p => p.Text));
            var finish = candidate?.FinishReason;
            var usage = chunk.UsageMetadata;

            if (string.IsNullOrEmpty(text) && finish is null && usage is null) continue;

            yield return new ProviderStreamChunk
            {
                Content = text,
                IsFinal = finish is not null,
                FinishReason = finish is null ? null : MapFinishReason(finish),
                Usage = usage is null ? null : new TokenUsage(usage.PromptTokenCount, usage.CandidatesTokenCount)
            };
        }
    }

    private static GoogleGenerateRequest MapToGoogle(ProviderRequest request)
    {
        var ext = request.Extensions as GoogleExtensions;

        GoogleContent? systemInstruction = null;
        var contents = new List<GoogleContent>(request.Messages.Count);

        foreach (var m in request.Messages)
        {
            if (m.Role == MessageRole.System)
            {
                systemInstruction ??= new GoogleContent { Parts = new List<GooglePart>() };
                systemInstruction.Parts.Add(new GooglePart { Text = m.Content });
                continue;
            }

            contents.Add(MapMessageToGoogle(m));
        }

        var generationConfig = new GoogleGenerationConfig
        {
            Temperature = request.Temperature,
            MaxOutputTokens = request.MaxTokens,
            TopP = ext?.TopP,
            TopK = ext?.TopK,
            CandidateCount = ext?.CandidateCount,
            StopSequences = request.StopSequences?.ToList()
        };

        if (request.ResponseSchema is { } spec)
        {
            generationConfig.ResponseMimeType = "application/json";

            if (!string.IsNullOrEmpty(spec.SchemaJson))
            {
                // Gemini's responseSchema is an OpenAPI-3.0 subset; rewrite the incoming
                // draft-2020-12 schema (union types, $schema, etc.) into a compatible shape.
                generationConfig.ResponseSchema = GoogleSchemaSanitizer.Sanitize(spec.SchemaJson);
            }
        }

        return new GoogleGenerateRequest
        {
            Contents = contents,
            SystemInstruction = systemInstruction,
            GenerationConfig = generationConfig,
            Tools = MapGoogleTools(request.Tools),
            ToolConfig = MapGoogleToolConfig(request.ToolChoice, request.Tools)
        };
    }

    private static GoogleContent MapMessageToGoogle(Message m)
    {
        // Tool result message: emit role "function" with a functionResponse part.
        if (m.Role == MessageRole.Tool)
        {
            using var responseDoc = JsonDocument.Parse(BuildResponseObjectJson(m.Content));
            // ToolCallId on Plume's side carries the id we synthesized; we round-trip
            // the call's name from the prior assistant turn via the id prefix
            // ("<name>-<index>"). Strip the index suffix to recover the name.
            var name = ExtractFunctionName(m.ToolCallId);
            return new GoogleContent
            {
                Role = "function",
                Parts = new List<GooglePart>
                {
                    new() { FunctionResponse = new GoogleFunctionResponse { Name = name, Response = responseDoc.RootElement.Clone() } }
                }
            };
        }

        var role = m.Role switch
        {
            MessageRole.User => "user",
            MessageRole.Assistant => "model",
            _ => "user"
        };

        // Assistant turn carrying tool calls: emit text + functionCall parts.
        if (m.Role == MessageRole.Assistant && m.ToolCalls is { Count: > 0 } calls)
        {
            var parts = new List<GooglePart>();
            if (!string.IsNullOrEmpty(m.Content))
                parts.Add(new GooglePart { Text = m.Content });
            foreach (var c in calls)
            {
                JsonElement? args = null;
                if (!string.IsNullOrEmpty(c.ArgumentsJson))
                {
                    using var argsDoc = JsonDocument.Parse(c.ArgumentsJson);
                    args = argsDoc.RootElement.Clone();
                }
                parts.Add(new GooglePart
                {
                    FunctionCall = new GoogleFunctionCall { Name = c.Name, Args = args }
                });
            }
            return new GoogleContent { Role = role, Parts = parts };
        }

        return new GoogleContent
        {
            Role = role,
            Parts = new List<GooglePart> { new() { Text = m.Content } }
        };
    }

    private static string BuildResponseObjectJson(string content)
    {
        // Gemini expects functionResponse.response to be an object. If the content is
        // already a JSON object, pass it through; otherwise wrap as { "result": <content> }.
        if (!string.IsNullOrEmpty(content))
        {
            try
            {
                using var probe = JsonDocument.Parse(content);
                if (probe.RootElement.ValueKind == JsonValueKind.Object) return content;
            }
            catch (JsonException) { /* fall through */ }
        }

        using var buf = new System.IO.MemoryStream();
        using (var w = new Utf8JsonWriter(buf))
        {
            w.WriteStartObject();
            w.WriteString("result", content);
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buf.ToArray());
    }

    private static string ExtractFunctionName(string? toolCallId)
    {
        if (string.IsNullOrEmpty(toolCallId)) return "function";
        var dash = toolCallId.LastIndexOf('-');
        return dash > 0 ? toolCallId[..dash] : toolCallId;
    }

    private static List<GoogleToolset>? MapGoogleTools(IReadOnlyList<Tool>? tools)
    {
        if (tools is null || tools.Count == 0) return null;
        var decls = new List<GoogleFunctionDeclaration>(tools.Count);
        foreach (var t in tools)
        {
            decls.Add(new GoogleFunctionDeclaration
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = GoogleSchemaSanitizer.Sanitize(t.ParametersJsonSchema)
            });
        }
        return new List<GoogleToolset> { new() { FunctionDeclarations = decls } };
    }

    private static GoogleToolConfig? MapGoogleToolConfig(ToolChoice? choice, IReadOnlyList<Tool>? tools)
    {
        if (tools is null || tools.Count == 0) return null;
        var (mode, allowed) = choice switch
        {
            null or ToolChoice.Auto => ("AUTO", null),
            ToolChoice.None => ("NONE", null),
            ToolChoice.Required => ("ANY", null),
            ToolChoice.Specific s => ("ANY", (List<string>?)new List<string> { s.Name }),
            _ => ("AUTO", null)
        };
        return new GoogleToolConfig
        {
            FunctionCallingConfig = new GoogleFunctionCallingConfig { Mode = mode, AllowedFunctionNames = allowed }
        };
    }

    private static FinishReason MapFinishReason(string? reason) => reason switch
    {
        "STOP" => FinishReason.Stop,
        "MAX_TOKENS" => FinishReason.Length,
        "SAFETY" => FinishReason.ContentFilter,
        "RECITATION" => FinishReason.ContentFilter,
        null => FinishReason.Stop,
        _ => FinishReason.Other
    };
}

/// <summary>Configuration for <see cref="GoogleProvider"/>.</summary>
public sealed class GoogleProviderOptions
{
    /// <summary>Google API key (from Google AI Studio).</summary>
    public required string ApiKey { get; init; }

    /// <summary>Base URL.</summary>
    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com";

    /// <summary>API version segment, e.g. "beta".</summary>
    public string ApiVersion { get; init; } = "v1beta";
}
