using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TMPublisher.Platforms.Bluesky;

/// <summary>
/// Rich text facets: Bluesky shows links and hashtags as links only when the post marks them, with UTF-8 byte offsets
/// (app.bsky.richtext.facet: #link and #tag).
/// </summary>
public static partial class BlueskyFacets
{
    /// <summary>Facets of the text (links and hashtags).</summary>
    public static JsonArray For(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var facets = new JsonArray();
        foreach (Match m in Link().Matches(text))
        {
            var url = m.Value.TrimEnd('.', ',', ')', '!', '?', ':', ';');
            facets.Add(Facet(text, m.Index, url.Length, new JsonObject { ["$type"] = "app.bsky.richtext.facet#link", ["uri"] = url }));
        }

        foreach (Match m in Tag().Matches(text))
        {
            var tag = m.Groups[1].Value;
            if (tag.Length is 0 or > 64)
            {
                continue;
            }

            facets.Add(Facet(text, m.Groups[1].Index - 1, tag.Length + 1, new JsonObject { ["$type"] = "app.bsky.richtext.facet#tag", ["tag"] = tag }));
        }

        return facets;
    }

    private static JsonObject Facet(string text, int start, int length, JsonObject feature) => new()
    {
        ["index"] = new JsonObject
        {
            ["byteStart"] = Encoding.UTF8.GetByteCount(text.AsSpan(0, start)),
            ["byteEnd"] = Encoding.UTF8.GetByteCount(text.AsSpan(0, start + length)),
        },
        ["features"] = new JsonArray(feature),
    };

    [GeneratedRegex(@"https?://[^\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex Link();

    [GeneratedRegex(@"(?:^|\s)#([\p{L}\p{N}_]+)")]
    private static partial Regex Tag();
}
