using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Plume.StructuredOutput;

namespace Plume.Tools;

/// <summary>
/// Strongly-typed factory for <see cref="BoundTool"/>. Auto-derives the parameters
/// JSON Schema from a <see cref="JsonTypeInfo{TArgs}"/> on .NET 9+, or accepts an
/// explicit schema string (and on .NET 8 — required there since automatic
/// derivation is unavailable).
/// </summary>
public static class ToolBinder
{
    /// <summary>
    /// Bind a tool with strongly-typed arguments and a string-returning handler.
    /// The parameters JSON Schema is derived from <paramref name="argsTypeInfo"/>
    /// (requires .NET 9+); on .NET 8 use the overload that accepts an explicit schema.
    /// </summary>
    public static BoundTool Bind<TArgs>(
        string name,
        string description,
        JsonTypeInfo<TArgs> argsTypeInfo,
        Func<TArgs, CancellationToken, Task<string>> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(argsTypeInfo);
        ArgumentNullException.ThrowIfNull(handler);

        var schema = JsonSchemaGenerator.TryGenerate(argsTypeInfo)
            ?? throw new InvalidOperationException(
                "JSON schema generation is unavailable on this runtime " +
                "(requires .NET 9+). Use the overload that accepts an explicit schema.");

        return BindCore(name, description, schema, argsTypeInfo, handler);
    }

    /// <summary>
    /// Bind a tool with strongly-typed arguments, an explicit JSON Schema, and a
    /// string-returning handler.
    /// </summary>
    public static BoundTool Bind<TArgs>(
        string name,
        string description,
        string parametersJsonSchema,
        JsonTypeInfo<TArgs> argsTypeInfo,
        Func<TArgs, CancellationToken, Task<string>> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(parametersJsonSchema);
        ArgumentNullException.ThrowIfNull(argsTypeInfo);
        ArgumentNullException.ThrowIfNull(handler);

        return BindCore(name, description, parametersJsonSchema, argsTypeInfo, handler);
    }

    /// <summary>
    /// Bind a tool with strongly-typed arguments and a typed result. The result
    /// is serialized via <paramref name="resultTypeInfo"/> before being returned
    /// to the model. Schema is derived from <paramref name="argsTypeInfo"/> (.NET 9+).
    /// </summary>
    public static BoundTool Bind<TArgs, TResult>(
        string name,
        string description,
        JsonTypeInfo<TArgs> argsTypeInfo,
        JsonTypeInfo<TResult> resultTypeInfo,
        Func<TArgs, CancellationToken, Task<TResult>> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(argsTypeInfo);
        ArgumentNullException.ThrowIfNull(resultTypeInfo);
        ArgumentNullException.ThrowIfNull(handler);

        var schema = JsonSchemaGenerator.TryGenerate(argsTypeInfo)
            ?? throw new InvalidOperationException(
                "JSON schema generation is unavailable on this runtime " +
                "(requires .NET 9+). Use the overload that accepts an explicit schema.");

        return BindCore(name, description, schema, argsTypeInfo, resultTypeInfo, handler);
    }

    /// <summary>
    /// Bind a tool with strongly-typed arguments, an explicit JSON Schema, and
    /// a typed result.
    /// </summary>
    public static BoundTool Bind<TArgs, TResult>(
        string name,
        string description,
        string parametersJsonSchema,
        JsonTypeInfo<TArgs> argsTypeInfo,
        JsonTypeInfo<TResult> resultTypeInfo,
        Func<TArgs, CancellationToken, Task<TResult>> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(parametersJsonSchema);
        ArgumentNullException.ThrowIfNull(argsTypeInfo);
        ArgumentNullException.ThrowIfNull(resultTypeInfo);
        ArgumentNullException.ThrowIfNull(handler);

        return BindCore(name, description, parametersJsonSchema, argsTypeInfo, resultTypeInfo, handler);
    }

    private static BoundTool BindCore<TArgs>(
        string name,
        string description,
        string schema,
        JsonTypeInfo<TArgs> argsTypeInfo,
        Func<TArgs, CancellationToken, Task<string>> handler)
    {
        var tool = new Tool { Name = name, Description = description, ParametersJsonSchema = schema };
        return new BoundTool(tool, async (argsJson, ct) =>
        {
            var parsed = ParseArgs(argsJson, argsTypeInfo, name);
            return await handler(parsed, ct).ConfigureAwait(false);
        });
    }

    private static BoundTool BindCore<TArgs, TResult>(
        string name,
        string description,
        string schema,
        JsonTypeInfo<TArgs> argsTypeInfo,
        JsonTypeInfo<TResult> resultTypeInfo,
        Func<TArgs, CancellationToken, Task<TResult>> handler)
    {
        var tool = new Tool { Name = name, Description = description, ParametersJsonSchema = schema };
        return new BoundTool(tool, async (argsJson, ct) =>
        {
            var parsed = ParseArgs(argsJson, argsTypeInfo, name);
            var result = await handler(parsed, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, resultTypeInfo);
        });
    }

    private static TArgs ParseArgs<TArgs>(string argsJson, JsonTypeInfo<TArgs> argsTypeInfo, string toolName)
    {
        var parsed = string.IsNullOrWhiteSpace(argsJson)
            ? default
            : JsonSerializer.Deserialize(argsJson, argsTypeInfo);
        if (parsed is null)
            throw new InvalidOperationException(
                $"Tool '{toolName}' received empty or null arguments.");
        return parsed;
    }
}
