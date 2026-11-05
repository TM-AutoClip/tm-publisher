using System.Text.RegularExpressions;

namespace TMPublisher.Core.Text;

/// <summary>
/// Hashtag rules in one place, so what the compose screen counts is what the publisher sends. The published value is ALWAYS what the form
/// shows; nothing is merged with another list at send time. A tag already in the text is not added twice.
/// </summary>
public static partial class HashtagRules
{
    /// <summary>
    /// Turns a keyword into a hashtag: only letters, digits and underscore stay ("my channel" becomes "#mychannel", "gta-rp" becomes
    /// "#gtarp"). A dash would make platforms see "#gta" only.
    /// </summary>
    public static string Tag(string? word)
    {
        var t = new string((word ?? string.Empty).Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        return t.Length == 0 ? string.Empty : "#" + t;
    }

    /// <summary>The keyword without '#' and surrounding spaces (YouTube's keyword field).</summary>
    public static string Keyword(string? word) => (word ?? string.Empty).Trim().TrimStart('#').Trim();

    /// <summary>Title and body (blank line between), then the hashtags of the words not already in the text.</summary>
    public static string TitledText(string? title, string? body, IEnumerable<string>? words)
    {
        var parts = new[] { title, body }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim());
        return AppendTags(string.Join("\n\n", parts), words);
    }

    /// <summary>Hashtags in the text in order (repeats included).</summary>
    public static List<string> TagsIn(string? text) => TagPattern().Matches(text ?? string.Empty).Select(m => m.Value).ToList();

    /// <summary>Appends the hashtags of the words that are not in the text yet (case-insensitive).</summary>
    public static string AppendTags(string? body, IEnumerable<string>? words)
    {
        var text = (body ?? string.Empty).TrimEnd();
        var existing = new HashSet<string>(TagsIn(text), StringComparer.OrdinalIgnoreCase);
        var add = new List<string>();
        foreach (var word in words ?? [])
        {
            var tag = Tag(word);
            if (tag.Length > 0 && existing.Add(tag))
            {
                add.Add(tag);
            }
        }

        if (add.Count == 0)
        {
            return text;
        }

        var line = string.Join(" ", add);
        return text.Length > 0 ? text + "\n\n" + line : line;
    }

    [GeneratedRegex(@"#[\p{L}\p{N}_]+")]
    private static partial Regex TagPattern();
}
