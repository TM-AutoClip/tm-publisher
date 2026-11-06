using TMPublisher.Core.Security;

namespace TMPublisher.Platforms.Common;

/// <summary>
/// Host names of the platforms (and their sub-domains) that may appear in the log as they are. Any other host (a Mastodon instance, a
/// Bluesky server the user chose: its name may be the user's own domain) is written as <see cref="Other"/>.
/// </summary>
public static class PlatformHosts
{
    /// <summary>What a host outside <see cref="Known"/> is written as (the same text the log masker writes for a registered one).</summary>
    public const string Other = AccountHosts.Masked;

    /// <summary>The known domains (github.com: the program's own bug form).</summary>
    public static readonly IReadOnlyList<string> Known =
    [
        "googleapis.com", "google.com", "youtube.com", "x.com", "twitter.com", "tiktokapis.com", "tiktok.com", "facebook.com", "instagram.com",
        "threads.net", "telegram.org", "discord.com", "discordapp.com", "bsky.social", "bsky.network", "bsky.app", "pinterest.com", "linkedin.com",
        "github.com",
    ];

    /// <summary>
    /// The host of <paramref name="address"/> as it may be logged: a known one as it is, any other as <see cref="Other"/>. Any other is also
    /// registered with <see cref="AccountHosts"/>, so the log masker hides it where an exception message carries it.
    /// </summary>
    public static string Display(Uri? address)
    {
        if (address is null || !address.IsAbsoluteUri)
        {
            return Other;
        }

        var host = address.Host.ToLowerInvariant();
        if (Known.Any(k => host == k || host.EndsWith("." + k, StringComparison.Ordinal)))
        {
            return host;
        }

        AccountHosts.Add(host);
        return Other;
    }
}
