using System.Text.Json;
using System.Text.Json.Nodes;

namespace TMPublisher.Platforms.Common;

/// <summary>JSON parsing that does not throw on HTML or empty answers.</summary>
public static class JsonSafe
{
    /// <summary>The node, or <c>null</c> when the text is not JSON.</summary>
    public static JsonNode? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A string member, or empty.</summary>
    public static string Str(JsonNode? node, string name) => node?[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : node?[name]?.ToString() ?? string.Empty;
}
