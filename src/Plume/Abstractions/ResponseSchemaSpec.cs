namespace Plume.Abstractions;

/// <summary>
/// Provider-agnostic structured output specification.
/// When set on a <see cref="ProviderRequest"/>, providers should constrain the model
/// to produce JSON conforming to <see cref="SchemaJson"/> (or plain JSON if null).
/// </summary>
public sealed record ResponseSchemaSpec
{
    /// <summary>
    /// JSON Schema (Draft 2020-12) describing the desired response shape.
    /// If null, the provider should request plain JSON output without enforcement.
    /// </summary>
    public string? SchemaJson { get; init; }

    /// <summary>
    /// Optional identifier for the schema. OpenAI uses this for the
    /// <c>json_schema.name</c> field; ignored by other providers.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// If true, the provider should reject any output that doesn't conform.
    /// Honored where the provider supports strict mode (OpenAI <c>strict: true</c>,
    /// Gemini's responseSchema, Ollama's format).
    /// </summary>
    public bool Strict { get; init; } = true;
}
