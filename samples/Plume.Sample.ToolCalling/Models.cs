using System.Text.Json.Serialization;

namespace Plume.Sample.ToolCalling;

// Each tool's input/output is a typed record. JsonTypeInfo from the source generator
// gives us AOT-safe serialization plus auto-derived JSON Schema on .NET 9+.

public sealed record GetWeatherArgs(string City);
public sealed record WeatherResult(string City, double TempC, string Conditions);

public sealed record CalculateArgs(double A, double B, string Operator);
public sealed record CalculateResult(double Result, string Expression);

public sealed record SearchKnowledgeArgs(string Query, int MaxResults);
public sealed record SearchHit(string Title, string Snippet);
public sealed record SearchResults(SearchHit[] Hits);

[JsonSerializable(typeof(GetWeatherArgs))]
[JsonSerializable(typeof(WeatherResult))]
[JsonSerializable(typeof(CalculateArgs))]
[JsonSerializable(typeof(CalculateResult))]
[JsonSerializable(typeof(SearchKnowledgeArgs))]
[JsonSerializable(typeof(SearchResults))]
public sealed partial class ToolsContext : JsonSerializerContext;
