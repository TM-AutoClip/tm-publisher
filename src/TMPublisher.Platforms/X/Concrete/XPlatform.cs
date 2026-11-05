using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.X;

/// <summary>
/// X: the user's own app in the X developer console (OAuth 2.0, native app = public client, no secret); loopback + PKCE; text, up to four
/// photos or one video. Pay per use: the compose screen shows the estimated cost (a post with a link costs more).
/// </summary>
public sealed class XPlatform : PlatformBase
{
    /// <summary>Loopback port registered for X.</summary>
    public const int LoopbackPort = 47812;

    /// <summary>Creates.</summary>
    public XPlatform(HttpTransport transport, TimeProvider clock, ILoopbackListener listener, ILoggerFactory loggers)
    {
        ArgumentNullException.ThrowIfNull(loggers);
        var refresher = new XTokenRefresher(transport, clock);
        Refresher = refresher;
        Connection = new XConnection(transport, clock, listener);
        Publisher = new XPublisher(transport, loggers.CreateLogger<XPublisher>());
    }

    /// <inheritdoc />
    public override string Code => PlatformCodes.X;

    /// <inheritdoc />
    public override string DisplayName => "X";

    /// <inheritdoc />
    public override string Color => "#2B2F38";

    /// <inheritdoc />
    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Text, PostKind.Photo, PostKind.Video },
        MaxTextLength = XText.MaxWeight,
        TextMeasure = XWeightedMeasure.Instance,
        MaxPhotos = 4,
        MaxPhotoBytes = 5L * 1024 * 1024,
        MaxVideoBytes = 512L * 1024 * 1024,
        Quotas = [new QuotaRule(100, QuotaWindow.Rolling15Minutes)],
        CostPerPost = 0.015m,
        CostPerLinkPost = 0.20m,
        UnsupportedReasons = new Dictionary<PostKind, string> { [PostKind.Album] = "reason.no_mixed" },
    };

    /// <inheritdoc />
    public override IConnectionMethod Connection { get; }

    /// <inheritdoc />
    public override SetupGuide Guide => Steps(6,
        new Dictionary<int, Uri> { [1] = new("https://console.x.com"), [5] = new("https://docs.x.com/x-api/getting-started/pricing") },
        new Dictionary<int, string> { [3] = Core.Accounts.CredentialKeys.RedirectUri }, "warning.x.cost");

    /// <inheritdoc />
    public override IPublisher Publisher { get; }

    /// <inheritdoc />
    public override ITokenRefresher? Refresher { get; }
}
