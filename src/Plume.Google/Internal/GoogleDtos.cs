using System.Text.Json.Serialization;

namespace Plume.Google.Internal;

internal sealed class GoogleGenerateRequest
{
    [JsonPropertyName("contents")]
    public List<GoogleContent> Contents { get; set; } = new();

    [JsonPropertyName("systemInstruction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GoogleContent? SystemInstruction { get; set; }

    [JsonPropertyName("generationConfig")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GoogleGenerationConfig? GenerationConfig { get; set; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GoogleToolset>? Tools { get; set; }

    [JsonPropertyName("toolConfig")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GoogleToolConfig? ToolConfig { get; set; }
}

internal sealed class GoogleToolset
{
    [JsonPropertyName("functionDeclarations")]
    public List<GoogleFunctionDeclaration> FunctionDeclarations { get; set; } = new();
}

internal sealed class GoogleFunctionDeclaration
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    public System.Text.Json.JsonElement Parameters { get; set; }
}

internal sealed class GoogleToolConfig
{
    [JsonPropertyName("functionCallingConfig")]
    public GoogleFunctionCallingConfig FunctionCallingConfig { get; set; } = new();
}

internal sealed class GoogleFunctionCallingConfig
{
    /// <summary>"AUTO" | "NONE" | "ANY"</summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "AUTO";

    [JsonPropertyName("allowedFunctionNames")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowedFunctionNames { get; set; }
}

internal sealed class GoogleContent
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    [JsonPropertyName("parts")]
    public List<GooglePart> Parts { get; set; } = new();
}

internal sealed class GooglePart
{
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("functionCall")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GoogleFunctionCall? FunctionCall { get; set; }

    [JsonPropertyName("functionResponse")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GoogleFunctionResponse? FunctionResponse { get; set; }
}

internal sealed class GoogleFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("args")]
    public System.Text.Json.JsonElement? Args { get; set; }
}

internal sealed class GoogleFunctionResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("response")]
    public System.Text.Json.JsonElement Response { get; set; }
}

internal sealed class GoogleGenerationConfig
{
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxOutputTokens { get; set; }

    [JsonPropertyName("topP")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopP { get; set; }

    [JsonPropertyName("topK")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TopK { get; set; }

    [JsonPropertyName("candidateCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CandidateCount { get; set; }

    [JsonPropertyName("stopSequences")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? StopSequences { get; set; }

    [JsonPropertyName("responseMimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResponseMimeType { get; set; }

    [JsonPropertyName("responseSchema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public System.Text.Json.JsonElement? ResponseSchema { get; set; }
}

internal sealed class GoogleGenerateResponse
{
    [JsonPropertyName("candidates")]
    public List<GoogleCandidate>? Candidates { get; set; }

    [JsonPropertyName("modelVersion")]
    public string? ModelVersion { get; set; }

    [JsonPropertyName("usageMetadata")]
    public GoogleUsageMetadata? UsageMetadata { get; set; }
}

internal sealed class GoogleCandidate
{
    [JsonPropertyName("content")]
    public GoogleContent? Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }

    [JsonPropertyName("index")]
    public int? Index { get; set; }
}

internal sealed class GoogleUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }

    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; set; }
}

internal sealed class GoogleBatchEmbedRequest
{
    [JsonPropertyName("requests")]
    public List<GoogleEmbedSingleRequest> Requests { get; set; } = new();
}

internal sealed class GoogleEmbedSingleRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("content")]
    public GoogleContent Content { get; set; } = new();

    [JsonPropertyName("outputDimensionality")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? OutputDimensionality { get; set; }
}

internal sealed class GoogleBatchEmbedResponse
{
    [JsonPropertyName("embeddings")]
    public List<GoogleEmbeddingValues>? Embeddings { get; set; }
}

internal sealed class GoogleEmbeddingValues
{
    [JsonPropertyName("values")]
    public float[]? Values { get; set; }
}

[JsonSerializable(typeof(GoogleGenerateRequest))]
[JsonSerializable(typeof(GoogleGenerateResponse))]
[JsonSerializable(typeof(GoogleBatchEmbedRequest))]
[JsonSerializable(typeof(GoogleBatchEmbedResponse))]
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class GoogleJsonContext : JsonSerializerContext { }
