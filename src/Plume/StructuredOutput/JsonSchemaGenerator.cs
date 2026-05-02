using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

#if NET9_0_OR_GREATER
using System.Text.Json.Schema;
#endif

namespace Plume.StructuredOutput;

/// <summary>
/// Derives a JSON Schema string from a <see cref="JsonTypeInfo"/>.
/// Uses <c>System.Text.Json.Schema.JsonSchemaExporter</c> on .NET 9+; on .NET 8
/// this returns null so providers fall back to plain JSON mode.
/// </summary>
public static class JsonSchemaGenerator
{
    /// <summary>
    /// Generate a JSON Schema string for the given <paramref name="typeInfo"/>,
    /// or null if schema generation is unavailable on the current runtime.
    /// </summary>
    public static string? TryGenerate(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

#if NET9_0_OR_GREATER
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo);
        return node.ToJsonString();
#else
        return null;
#endif
    }

    /// <summary>
    /// Generate a JSON Schema string from the supplied <see cref="JsonSerializerOptions"/>
    /// for type <typeparamref name="T"/>, or null if schema generation is unavailable.
    /// </summary>
    public static string? TryGenerate<T>(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

#if NET9_0_OR_GREATER
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(options, typeof(T));
        return node.ToJsonString();
#else
        return null;
#endif
    }
}
