using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Plume.Abstractions;

namespace Plume.StructuredOutput;

/// <summary>
/// Typed structured-output extensions for <see cref="IPlumeClient"/>.
/// Generates a JSON schema from a <see cref="JsonTypeInfo{T}"/>, asks the model
/// to produce conformant JSON, and deserializes the response — all AOT-safe.
/// </summary>
public static class PlumeClientStructuredExtensions
{
    /// <summary>
    /// Send <paramref name="prompt"/> and receive a typed response of <typeparamref name="T"/>.
    /// On .NET 9+ a JSON schema is derived from <paramref name="jsonTypeInfo"/> and
    /// passed to the provider for strict enforcement; on .NET 8 the call falls
    /// back to plain JSON mode (no schema enforcement). To override the derived
    /// schema, set <see cref="AskOptions.ResponseSchema"/> on <paramref name="options"/>.
    /// </summary>
    /// <exception cref="PlumeStructuredOutputException">
    /// Thrown when the provider returns content that cannot be parsed as <typeparamref name="T"/>.
    /// </exception>
    public static async Task<T> AskAsync<T>(
        this IPlumeClient client,
        string prompt,
        JsonTypeInfo<T> jsonTypeInfo,
        AskOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrEmpty(prompt);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        // Caller-supplied schema wins; otherwise derive from JsonTypeInfo (net9+).
        var explicitSpec = options?.ResponseSchema;
        var schemaJson = explicitSpec?.SchemaJson ?? JsonSchemaGenerator.TryGenerate(jsonTypeInfo);
        if (schemaJson is null)
            throw new PlumeStructuredOutputException(
                "JSON schema generation is unavailable on this runtime (requires .NET 9+). " +
                "Either target net9.0+ or pass an explicit schema via AskOptions.ResponseSchema.");

        var messages = new List<Message>(2);
        if (!string.IsNullOrWhiteSpace(options?.System))
            messages.Add(new Message(MessageRole.System, options.System));
        messages.Add(new Message(MessageRole.User, prompt));

        var request = new PlumeRequest
        {
            Messages = messages,
            Model = options?.Model,
            Temperature = options?.Temperature,
            MaxTokens = options?.MaxTokens,
            StopSequences = options?.StopSequences,
            Extensions = options?.Extensions,
            ResponseSchema = new ResponseSchemaSpec
            {
                SchemaJson = schemaJson,
                Name = explicitSpec?.Name ?? SanitizeSchemaName(typeof(T).Name),
                Strict = explicitSpec?.Strict ?? true
            }
        };

        var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(response.Content))
            throw new PlumeStructuredOutputException(
                "Provider returned an empty body — no JSON to deserialize.");

        try
        {
            var value = JsonSerializer.Deserialize(response.Content, jsonTypeInfo);
            if (value is null)
                throw new PlumeStructuredOutputException(
                    $"Provider returned JSON 'null' which cannot be assigned to {typeof(T)}.");
            return value;
        }
        catch (JsonException ex)
        {
            throw new PlumeStructuredOutputException(
                $"Failed to parse provider response as {typeof(T)}: {ex.Message}", ex);
        }
    }

    private static string SanitizeSchemaName(string typeName)
    {
        // OpenAI requires names to match ^[a-zA-Z0-9_-]+$ with length 1..64.
        Span<char> buf = stackalloc char[Math.Min(typeName.Length, 64)];
        var w = 0;
        foreach (var c in typeName)
        {
            if (w >= buf.Length) break;
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                buf[w++] = c;
        }
        return w == 0 ? "response" : new string(buf[..w]);
    }
}

/// <summary>
/// Thrown when a provider returns content that cannot be parsed as the
/// requested structured-output type.
/// </summary>
public sealed class PlumeStructuredOutputException : PlumeException
{
    /// <summary>Create a new structured-output exception.</summary>
    public PlumeStructuredOutputException(string message) : base(message) { }

    /// <summary>Create a new structured-output exception with an inner cause.</summary>
    public PlumeStructuredOutputException(string message, Exception inner) : base(message, inner) { }
}
