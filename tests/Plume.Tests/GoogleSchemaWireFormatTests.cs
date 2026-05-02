using System.Net;
using System.Text.Json;
using Plume.Abstractions;
using Plume.Google;
using Plume.Tools;
using Xunit;

namespace Plume.Tests;

/// <summary>
/// Pins the wire format Plume sends to Gemini for schema-bearing requests.
/// These tests would have caught the two recent bugs:
///   - draft-2020-12 union types ("type": ["string","null"]) being passed as-is
///   - tool parameter schemas not getting the same sanitization treatment
/// They run on net8 — no JsonSchemaExporter required — by hand-crafting the
/// kind of schema System.Text.Json's exporter emits on net9+.
/// </summary>
public class GoogleSchemaWireFormatTests
{
    private const string DraftSchemaWithUnions = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "Name": { "type": ["string", "null"] },
            "Servings": { "type": "integer" },
            "Ingredients": {
              "type": ["array", "null"],
              "items": { "type": ["string", "null"] }
            }
          },
          "$defs": { "leftover": { "type": "string" } }
        }
        """;

    [Fact]
    public async Task ResponseSchemaIsRewrittenToOpenApiSubsetBeforeSendingToGemini()
    {
        var (provider, recorder) = BuildProviderWithCapture();

        await provider.SendAsync(new ProviderRequest
        {
            Model = "gemini-2.5-flash",
            Messages = new[] { new Message(MessageRole.User, "hi") },
            ResponseSchema = new ResponseSchemaSpec
            {
                SchemaJson = DraftSchemaWithUnions,
                Name = "Recipe",
                Strict = true
            }
        }, CancellationToken.None);

        var schema = ExtractResponseSchema(recorder.LastBody!);
        AssertGeminiCompatible(schema);
    }

    [Fact]
    public async Task ToolParametersAreRewrittenToOpenApiSubsetBeforeSendingToGemini()
    {
        var (provider, recorder) = BuildProviderWithCapture();

        await provider.SendAsync(new ProviderRequest
        {
            Model = "gemini-2.5-flash",
            Messages = new[] { new Message(MessageRole.User, "hi") },
            Tools = new[]
            {
                new Tool
                {
                    Name = "make_recipe",
                    Description = "produces a recipe",
                    ParametersJsonSchema = DraftSchemaWithUnions
                }
            }
        }, CancellationToken.None);

        var toolParams = ExtractFirstToolParameters(recorder.LastBody!);
        AssertGeminiCompatible(toolParams);
    }

    private static void AssertGeminiCompatible(JsonElement schema)
    {
        // No keywords Gemini's responseSchema rejects.
        AssertKeywordAbsent(schema, "$schema");
        AssertKeywordAbsent(schema, "$defs");
        AssertKeywordAbsent(schema, "additionalProperties");

        // No "type" arrays anywhere — must be a single string + nullable: true.
        AssertNoTypeArrays(schema);

        // Specifically: the nullable string field must round-trip as nullable=true.
        var nameProp = schema.GetProperty("properties").GetProperty("Name");
        Assert.Equal(JsonValueKind.String, nameProp.GetProperty("type").ValueKind);
        Assert.Equal("string", nameProp.GetProperty("type").GetString());
        Assert.True(nameProp.GetProperty("nullable").GetBoolean());

        // Nested array items must be similarly rewritten.
        var items = schema.GetProperty("properties").GetProperty("Ingredients").GetProperty("items");
        Assert.Equal("string", items.GetProperty("type").GetString());
        Assert.True(items.GetProperty("nullable").GetBoolean());
    }

    private static void AssertKeywordAbsent(JsonElement el, string keyword)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                Assert.NotEqual(keyword, prop.Name);
                AssertKeywordAbsent(prop.Value, keyword);
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
                AssertKeywordAbsent(item, keyword);
        }
    }

    private static void AssertNoTypeArrays(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (prop.Name == "type")
                    Assert.NotEqual(JsonValueKind.Array, prop.Value.ValueKind);
                AssertNoTypeArrays(prop.Value);
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
                AssertNoTypeArrays(item);
        }
    }

    private static JsonElement ExtractResponseSchema(string requestBody)
    {
        using var doc = JsonDocument.Parse(requestBody);
        return doc.RootElement
            .GetProperty("generationConfig")
            .GetProperty("responseSchema")
            .Clone();
    }

    private static JsonElement ExtractFirstToolParameters(string requestBody)
    {
        using var doc = JsonDocument.Parse(requestBody);
        return doc.RootElement
            .GetProperty("tools")[0]
            .GetProperty("functionDeclarations")[0]
            .GetProperty("parameters")
            .Clone();
    }

    private static (GoogleProvider, RecordingHandler) BuildProviderWithCapture()
    {
        // Minimal valid Gemini reply so the provider's response parsing doesn't bail.
        const string fakeReply = """
            {
              "candidates": [{
                "content": { "parts": [{ "text": "ok" }] },
                "finishReason": "STOP"
              }]
            }
            """;
        var recorder = new RecordingHandler(fakeReply);
        var http = new HttpClient(recorder);
        var provider = new GoogleProvider(http, new GoogleProviderOptions { ApiKey = "test-key" });
        return (provider, recorder);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _replyJson;
        public string? LastBody { get; private set; }

        public RecordingHandler(string replyJson) => _replyJson = replyJson;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_replyJson, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
