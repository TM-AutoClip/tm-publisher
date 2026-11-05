using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.TikTok;

/// <summary>
/// TikTok with the user's own TikTok developer app (Login Kit for Desktop: loopback, PKCE with a hex challenge). Default: the video goes to
/// the TikTok inbox as a draft and the user finishes the post in the TikTok app; optional: a direct post, which an unaudited app can only
/// publish privately (SELF_ONLY). TikTok's audit does not accept "a tool to upload to my own accounts", so public direct posts stay closed.
/// </summary>
public sealed class TikTokPlatform : PlatformBase
{
    /// <summary>Loopback port registered for TikTok.</summary>
    public const int LoopbackPort = 47813;

    /// <summary>Creates.</summary>
    public TikTokPlatform(HttpTransport transport, TimeProvider clock, ILoopbackListener listener, ILoggerFactory loggers)
    {
        ArgumentNullException.ThrowIfNull(loggers);
        var refresher = new TikTokTokenRefresher(transport, clock);
        Refresher = refresher;
        Connection = new TikTokConnection(transport, clock, listener);
        Publisher = new TikTokPublisher(transport, loggers.CreateLogger<TikTokPublisher>());
    }

    /// <inheritdoc />
    public override string Code => PlatformCodes.TikTok;

    /// <inheritdoc />
    public override string DisplayName => "TikTok";

    /// <inheritdoc />
    public override string Color => "#2B2F38";

    /// <inheritdoc />
    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Video },
        MaxTextLength = 2200,
        TextMeasure = Utf16Measure.Instance,
        MaxVideoBytes = 4L * 1024 * 1024 * 1024,
        Quotas = [new QuotaRule(15, QuotaWindow.Rolling24Hours)],
        UnsupportedReasons = new Dictionary<PostKind, string>
        {
            [PostKind.Text] = "reason.tiktok_video_only",
            [PostKind.Photo] = "reason.tiktok_video_only",
            [PostKind.Album] = "reason.tiktok_video_only",
        },
    };

    /// <inheritdoc />
    public override IConnectionMethod Connection { get; }

    /// <inheritdoc />
    public override SetupGuide Guide => Steps(6,
        new Dictionary<int, Uri> { [1] = new("https://developers.tiktok.com/apps/") },
        new Dictionary<int, string> { [3] = Core.Accounts.CredentialKeys.RedirectUri },
        "warning.tiktok.drafts", "warning.tiktok.private", "warning.tiktok.audit");

    /// <inheritdoc />
    public override IPublisher Publisher { get; }

    /// <inheritdoc />
    public override ITokenRefresher? Refresher { get; }
}
