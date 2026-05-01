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
    public string Text { get; set; } = "";
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

[JsonSerializable(typeof(GoogleGenerateRequest))]
[JsonSerializable(typeof(GoogleGenerateResponse))]
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class GoogleJsonContext : JsonSerializerContext { }
