namespace TMPublisher.Platforms.Common;

/// <summary>
/// Host names of the platforms (and their sub-domains) that may appear in the log as they are. Any other host (a Mastodon instance, a
/// Bluesky server the user chose: its name may be the user's own domain) is written as <see cref="Other"/>.
/// </summary>
public static class PlatformHosts
{
    /// <summary>What a host outside <see cref="Known"/> is written as.</summary>
    public const string Other = "(account server)";

    /// <summary>The known domains.</summary>
    public static readonly IReadOnlyList<string> Known =
    [
        "googleapis.com", "google.com", "youtube.com", "x.com", "twitter.com", "tiktokapis.com", "tiktok.com", "facebook.com", "instagram.com",
        "threads.net", "telegram.org", "discord.com", "discordapp.com", "bsky.social", "bsky.network", "bsky.app", "pinterest.com", "linkedin.com",
    ];
}
