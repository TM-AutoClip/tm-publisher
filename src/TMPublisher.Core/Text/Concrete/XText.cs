using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TMPublisher.Core.Text;

/// <summary>
/// X post text rules (the composer and the publisher count the same way):
/// <list type="number">
/// <item><b>280 weighted characters</b> (docs.x.com/fundamentals/counting-characters and twitter-text config/v3.json): code points 0-4351,
/// 8192-8205, 8208-8223, 8242-8247 weigh 1; the rest 2; every emoji (skin tone, ZWJ family, flags included) 2; every link 23. The text is
/// normalized to NFC first. When in doubt it counts more. A longer text is never cut silently: the validator blocks it.</item>
/// <item><b>Links</b> (a simplified twitter-text): anything with <c>http(s)://</c>; an ASCII domain without a scheme ending in a top-level
/// domain X knows ("example.com", "abc.com.tr"); not when preceded by a letter, digit or <c>@ $ # - _ . /</c>; a two-part country
/// domain without a path is not a link (except co and tv). A post with a link costs more on X; the compose screen shows the estimate.</item>
/// </list>
/// </summary>
public static class XText
{
    /// <summary>Weighted character limit of a post.</summary>
    public const int MaxWeight = 280;

    /// <summary>Every link counts 23 whatever its length (t.co).</summary>
    public const int LinkWeight = 23;

    private static readonly Regex WithProtocol = new(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DomainCandidate = new(
        @"(?<![\p{L}\p{M}\p{N}_\-@＠$#＃./])[\p{L}\p{M}\p{N}_\-]+(?:\.[\p{L}\p{M}\p{N}_\-]+)+",
        RegexOptions.CultureInvariant);

    /// <summary>Whether X would turn something in the text into a link.</summary>
    public static bool ContainsLink(string? text) => Links(Nfc(text)).Count > 0;

    /// <summary>The length X counts (the 280 limit applies to this).</summary>
    public static int WeightedLength(string? text)
    {
        var n = Nfc(text);
        var total = 0;
        foreach (var step in Steps(n, Links(n)))
        {
            total += step.Weight;
        }

        return total;
    }

    /// <summary>Position of a link in the text.</summary>
    private readonly record struct Link(int Start, int Length)
    {
        public int End => Start + Length;
    }

    /// <summary>A piece of the weight calculation.</summary>
    private readonly record struct Step(int Start, int End, int Weight);

    private static List<Link> Links(string n)
    {
        var links = new List<Link>();
        foreach (Match m in WithProtocol.Matches(n))
        {
            links.Add(new Link(m.Index, m.Length));
        }

        foreach (Match m in DomainCandidate.Matches(n))
        {
            if (links.Any(l => m.Index < l.End && l.Start < m.Index + m.Length))
            {
                continue;
            }

            if (FromDomain(n, m) is { } link)
            {
                links.Add(link);
            }
        }

        links.Sort((a, b) => a.Start.CompareTo(b.Start));
        return links;
    }

    /// <summary>X's link from a domain candidate (<c>null</c> when none); the top-level domain is searched from the end.</summary>
    private static Link? FromDomain(string n, Match m)
    {
        if (m.Value[0] is '-' or '_')
        {
            return null;
        }

        var parts = m.Value.Split('.');
        var starts = new int[parts.Length];
        for (int k = 0, position = m.Index; k < parts.Length; k++)
        {
            starts[k] = position;
            position += parts[k].Length + 1;
        }

        var candidateEnd = m.Index + m.Length;
        char? after = candidateEnd < n.Length ? n[candidateEnd] : null;
        for (var i = parts.Length - 1; i >= 1; i--)
        {
            var part = parts[i];
            var u = 0;
            while (u < part.Length && AsciiDomainChar(part[u]))
            {
                u++;
            }

            if (u == 0)
            {
                continue;
            }

            char? next = u < part.Length ? part[u] : i < parts.Length - 1 ? '.' : after;
            if (next is '@' or '+')
            {
                continue;
            }

            var top = part[..u].ToLowerInvariant();
            var generic = XDomains.Generic.Contains(top) || top.StartsWith("xn--", StringComparison.Ordinal);
            var country = !generic && XDomains.Country.Contains(top);
            if (!generic && !country)
            {
                continue;
            }

            int names = 0, start = -1;
            for (var j = i - 1; j >= 0; j--)
            {
                var p = parts[j];
                var k = p.Length;
                while (k > 0 && LatinDomainChar(p[k - 1]))
                {
                    k--;
                }

                if (k == p.Length)
                {
                    break;
                }

                names++;
                start = starts[j] + k;
                if (k > 0)
                {
                    break;
                }
            }

            if (names == 0)
            {
                continue;
            }

            var path = u == part.Length && i == parts.Length - 1 && after == '/';
            if (country && names == 1 && !path && top is not ("co" or "tv"))
            {
                return null;
            }

            var end = starts[i] + u;
            if (path)
            {
                end = candidateEnd;
                while (end < n.Length && !char.IsWhiteSpace(n[end]))
                {
                    end++;
                }
            }

            return new Link(start, end - start);
        }

        return null;
    }

    private static bool AsciiDomainChar(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-';

    private static bool LatinDomainChar(char c) =>
        AsciiDomainChar(c)
        || c is (>= 'À' and <= 'Ö') or (>= 'Ø' and <= 'ö') or (>= 'ø' and <= 'ɏ')
            or 'ɓ' or 'ɔ' or 'ɖ' or 'ɗ' or 'ə' or 'ɛ' or 'ɣ' or 'ɨ' or 'ɯ'
            or 'ɲ' or 'ʉ' or 'ʋ' or 'ʻ' or (>= '̀' and <= 'ͯ') or (>= 'Ḁ' and <= 'ỿ');

    private static IEnumerable<Step> Steps(string n, List<Link> links)
    {
        int i = 0, b = 0;
        while (i < n.Length)
        {
            while (b < links.Count && links[b].Start < i)
            {
                b++;
            }

            if (b < links.Count && links[b].Start == i)
            {
                var link = links[b++];
                yield return new Step(i, link.End, LinkWeight);
                i = link.End;
                continue;
            }

            var length = StringInfo.GetNextTextElementLength(n, i);
            if (length <= 0)
            {
                length = 1;
            }

            yield return new Step(i, i + length, GraphemeWeight(n.AsSpan(i, length)));
            i += length;
        }
    }

    private static int GraphemeWeight(ReadOnlySpan<char> grapheme)
    {
        if (IsEmoji(grapheme))
        {
            return 2;
        }

        var total = 0;
        foreach (var rune in grapheme.EnumerateRunes())
        {
            total += CodePointWeight(rune.Value);
        }

        return total;
    }

    private static bool IsEmoji(ReadOnlySpan<char> grapheme)
    {
        bool emoji = false, textPresentation = false;
        foreach (var rune in grapheme.EnumerateRunes())
        {
            var k = rune.Value;
            textPresentation |= k == 0xFE0E;
            emoji |= EmojiPart(k);
        }

        return emoji && !textPresentation;
    }

    private static int CodePointWeight(int k) =>
        k <= 4351 || k is >= 8192 and <= 8205 or >= 8208 and <= 8223 or >= 8242 and <= 8247 ? 1 : 2;

    private static bool EmojiPart(int k) =>
        k is 0xFE0F or 0x20E3 or 0x00A9 or 0x00AE or 0x203C or 0x2049 or 0x2122 or 0x2139 or 0x24C2
            or 0x3030 or 0x303D or 0x3297 or 0x3299
            or (>= 0x2190 and <= 0x21FF) or (>= 0x2300 and <= 0x23FF) or (>= 0x25AA and <= 0x25FE)
            or (>= 0x2600 and <= 0x27BF) or (>= 0x2B00 and <= 0x2BFF)
            or (>= 0x1F000 and <= 0x1FAFF) or (>= 0xE0020 and <= 0xE007F);

    private static string Nfc(string? text)
    {
        var s = text ?? string.Empty;
        try
        {
            return s.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return s;
        }
    }
}
