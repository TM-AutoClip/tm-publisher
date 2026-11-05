using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.YouTube;

/// <summary>
/// YouTube: the user's own Google Cloud project with a "Desktop app" OAuth client; loopback sign-in with PKCE; videos only (a vertical
/// video up to 3 minutes becomes a Short on YouTube's side).
/// </summary>
public sealed class YouTubePlatform : PlatformBase
{
    /// <summary>Loopback port registered for YouTube.</summary>
    public const int LoopbackPort = 47811;

    /// <summary>Creates.</summary>
    public YouTubePlatform(HttpTransport transport, TimeProvider clock, ILoopbackListener listener, ILoggerFactory loggers)
    {
        ArgumentNullException.ThrowIfNull(loggers);
        var refresher = new GoogleTokenRefresher(transport, clock);
        Refresher = refresher;
        Connection = new YouTubeConnection(transport, clock, listener, refresher);
        Publisher = new YouTubePublisher(transport, loggers.CreateLogger<YouTubePublisher>());
    }

    /// <inheritdoc />
    public override string Code => PlatformCodes.YouTube;

    /// <inheritdoc />
    public override string DisplayName => "YouTube";

    /// <inheritdoc />
    public override string Color => "#E62117";

    /// <inheritdoc />
    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Video },
        HasTitle = true,
        MaxTitleLength = 100,
        HasKeywords = true,
        MaxKeywordsLength = 500,
        ForbiddenCharacters = "<>",
        MaxTextLength = 5000,
        TextMeasure = Utf8ByteMeasure.Instance,
        MaxHashtags = 60,
        MaxVideoBytes = 256L * 1024 * 1024 * 1024,
        SupportsPrivacy = true,
        Quotas = [new QuotaRule(100, QuotaWindow.PacificDay)],
        UnsupportedReasons = new Dictionary<PostKind, string>
        {
            [PostKind.Text] = "reason.youtube_video_only",
            [PostKind.Photo] = "reason.youtube_video_only",
            [PostKind.Album] = "reason.youtube_video_only",
        },
    };

    /// <inheritdoc />
    public override IConnectionMethod Connection { get; }

    /// <inheritdoc />
    public override SetupGuide Guide => Steps(7,
        new Dictionary<int, Uri>
        {
            [1] = new("https://console.cloud.google.com/projectcreate"),
            [2] = new("https://console.cloud.google.com/apis/library/youtube.googleapis.com"),
            [3] = new("https://console.cloud.google.com/auth/branding"),
            [4] = new("https://console.cloud.google.com/auth/audience"),
            [5] = new("https://console.cloud.google.com/auth/clients"),
        },
        null, "warning.youtube.private", "warning.youtube.quota", "warning.youtube.unverified");

    /// <inheritdoc />
    public override IPublisher Publisher { get; }

    /// <inheritdoc />
    public override ITokenRefresher? Refresher { get; }
}
