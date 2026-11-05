using System.Text.RegularExpressions;

namespace TMPublisher.Core.Text;

/// <summary>Counts characters with every http(s) link as 23 (Mastodon's rule).</summary>
public sealed partial class LinkAs23Measure : ITextMeasure
{
    /// <summary>Shared instance.</summary>
    public static LinkAs23Measure Instance { get; } = new();

    /// <inheritdoc />
    public int Measure(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var length = new System.Globalization.StringInfo(text).LengthInTextElements;
        foreach (Match link in Link().Matches(text))
        {
            length += 23 - new System.Globalization.StringInfo(link.Value).LengthInTextElements;
        }

        return length;
    }

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Link();
}
