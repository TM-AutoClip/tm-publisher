namespace TMPublisher.Core.Text;

/// <summary>A tag list typed on one line ("#a b, c"): split on spaces, commas and semicolons; written back with spaces.</summary>
public static class TagLine
{
    private static readonly char[] Separators = [' ', ',', ';', '\t', '\r', '\n'];

    /// <summary>Tags in order, duplicates (ignoring case and '#') removed.</summary>
    public static List<string> Parse(string? line)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return (line ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => HashtagRules.Keyword(t).Length > 0 && seen.Add(HashtagRules.Keyword(t)))
            .ToList();
    }

    /// <summary>One line.</summary>
    public static string Join(IEnumerable<string>? tags) => string.Join(' ', tags ?? []);
}
