using System.Text;

namespace TMPublisher.Platforms.LinkedIn;

/// <summary>
/// LinkedIn's "little" text format (the Posts API <c>commentary</c>): every reserved character
/// <c>| { } @ [ ] ( ) &lt; &gt; # \ * _ ~</c> must be escaped with a backslash, even outside markup, or LinkedIn cuts or mangles the text.
/// A <c>#</c> that starts a word (a hashtag: not inside a word, followed by a letter or digit) is kept, so it stays a hashtag.
/// </summary>
public static class LittleText
{
    private const string Reserved = "|{}@[]()<>#\\*_~";

    /// <summary>The text with the reserved characters escaped.</summary>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new StringBuilder(text.Length + 16);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (Reserved.Contains(c, StringComparison.Ordinal) && !IsHashtagStart(text, i))
            {
                result.Append('\\');
            }

            result.Append(c);
        }

        return result.ToString();
    }

    private static bool IsHashtagStart(string text, int index) =>
        text[index] == '#'
        && index + 1 < text.Length && char.IsLetterOrDigit(text[index + 1])
        && (index == 0 || !(char.IsLetterOrDigit(text[index - 1]) || text[index - 1] is '_' or '#' or '\\'));
}
