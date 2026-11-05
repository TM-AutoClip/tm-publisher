using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Meta;

/// <summary>
/// Instagram professional account through Facebook Login (the user's own Meta app and Page). Videos only in this version (Reels uploaded from
/// this computer): Instagram takes photos only from a public web address, which a desktop app without a server does not have.
/// </summary>
public sealed class InstagramPlatform(HttpTransport transport, TimeProvider clock, ILoggerFactory loggers) : PlatformBase
{
    /// <summary>Instagram's help page allows 5 hashtags per post (the developer page still says 30); the safe side.</summary>
    public const int MaxHashtags = 5;

    /// <inheritdoc />
    public override string Code => PlatformCodes.Instagram;

    /// <inheritdoc />
    public override string DisplayName => "Instagram";

    /// <inheritdoc />
    public override string Color => "#C13584";

    /// <inheritdoc />
    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Video },
        MaxTextLength = 2200,
        TextMeasure = Utf16Measure.Instance,
        MaxHashtags = MaxHashtags,
        MaxVideoBytes = 300L * 1024 * 1024,
        Quotas = [new QuotaRule(100, QuotaWindow.Rolling24Hours)],
        UnsupportedReasons = new Dictionary<PostKind, string>
        {
            [PostKind.Text] = "reason.instagram_video_only",
            [PostKind.Photo] = "reason.instagram_video_only",
            [PostKind.Album] = "reason.instagram_video_only",
        },
    };

    /// <inheritdoc />
    public override IConnectionMethod Connection { get; } = new InstagramConnection(transport, clock);

    /// <inheritdoc />
    public override SetupGuide Guide => Steps(6,
        new Dictionary<int, Uri>
        {
            [1] = new("https://help.instagram.com/502981923235522"),
            [2] = new("https://developers.facebook.com/apps/creation/"),
            [4] = new("https://developers.facebook.com/tools/explorer/"),
        },
        null, "warning.instagram.video_only", "warning.instagram.limits", "warning.meta.devmode");

    /// <inheritdoc />
    public override IPublisher Publisher { get; } = new InstagramPublisher(transport, loggers.CreateLogger<InstagramPublisher>());
}
