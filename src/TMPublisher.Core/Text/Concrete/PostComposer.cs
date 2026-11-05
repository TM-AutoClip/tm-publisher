using TMPublisher.Core.Platforms;

namespace TMPublisher.Core.Text;

/// <summary>
/// Builds the final text of one target from the form: the platform's default tags plus the post's own tags. Platforms with a title field
/// get the body and tags as text and the title separately; the others get the title on the first line. The compose screen shows this
/// exact result and stores it on the target; publishing sends it unchanged.
/// </summary>
public static class PostComposer
{
    /// <summary>Final text, title and keywords for a platform.</summary>
    public static (string Text, string Title, List<string> Keywords) Compose(PlatformCapabilities capabilities, string? title, string? body,
        IEnumerable<string>? platformTags, IEnumerable<string>? postTags)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        var words = Words(platformTags, postTags);
        if (capabilities.HasKeywords)
        {
            // YouTube: tags go to the keyword field; the hashtags stay in the description only when the user typed them there.
            var keywords = words.Select(HashtagRules.Keyword).Where(k => k.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return ((body ?? string.Empty).Trim(), (title ?? string.Empty).Trim(), keywords);
        }

        return capabilities.HasTitle
            ? (HashtagRules.AppendTags(body?.Trim(), words), (title ?? string.Empty).Trim(), [])
            : (HashtagRules.TitledText(title, body, words), string.Empty, []);
    }

    private static List<string> Words(IEnumerable<string>? first, IEnumerable<string>? second) =>
        (first ?? []).Concat(second ?? []).Select(w => w.Trim()).Where(w => w.Length > 0).ToList();
}
