using TMPublisher.Core.Platforms;

namespace TMPublisher.Core.Settings;

/// <summary>
/// Ready-made starting sets, so nobody has to type the usual short-video tags. Only widely used, neutral tags; the user edits them in
/// Settings and per post.
/// </summary>
public static class StarterTagSets
{
    /// <summary>Starting set of a platform (empty for platforms where tags are not customary).</summary>
    public static IReadOnlyList<string> For(string platform) => platform switch
    {
        PlatformCodes.YouTube => ["shorts"],
        PlatformCodes.TikTok => ["fyp", "foryou"],
        PlatformCodes.Instagram => ["reels"],
        _ => [],
    };
}
