using System.Text.Json.Serialization;

namespace Plume.Sample.StructuredOutput;

public sealed record Recipe(
    string Name,
    int Servings,
    int PrepTimeMinutes,
    Ingredient[] Ingredients,
    string[] Steps);

public sealed record Ingredient(string Name, double Quantity, string Unit);

[JsonSerializable(typeof(Recipe))]
public sealed partial class RecipeContext : JsonSerializerContext;

public sealed record Sentiment(string Label, double Confidence, string Reasoning);

[JsonSerializable(typeof(Sentiment))]
public sealed partial class SentimentContext : JsonSerializerContext;
