using System.Text.Json;
using System.Text.Json.Nodes;

namespace Plume.Google.Internal;

internal static class GoogleSchemaSanitizer
{
    public static JsonElement Sanitize(string schemaJson)
    {
        var node = JsonNode.Parse(schemaJson)
            ?? throw new JsonException("Schema JSON parsed to null.");
        Walk(node);
        using var doc = JsonDocument.Parse(node.ToJsonString());
        return doc.RootElement.Clone();
    }

    private static void Walk(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                NormalizeObject(obj);
                foreach (var kv in obj.ToList())
                    Walk(kv.Value);
                break;
            case JsonArray arr:
                foreach (var item in arr)
                    Walk(item);
                break;
        }
    }

    private static void NormalizeObject(JsonObject obj)
    {
        // Drop keywords Gemini's responseSchema rejects.
        obj.Remove("$schema");
        obj.Remove("additionalProperties");
        obj.Remove("$defs");
        obj.Remove("definitions");
        obj.Remove("$id");
        obj.Remove("$comment");

        // Convert draft-2020-12 union "type": ["foo", "null"] -> "type": "foo", "nullable": true.
        if (obj["type"] is JsonArray types)
        {
            string? primary = null;
            var nullable = false;
            foreach (var t in types)
            {
                var s = t?.GetValue<string>();
                if (s == "null") nullable = true;
                else primary ??= s;
            }
            obj.Remove("type");
            if (primary is not null) obj["type"] = primary;
            if (nullable) obj["nullable"] = true;
        }
    }
}
