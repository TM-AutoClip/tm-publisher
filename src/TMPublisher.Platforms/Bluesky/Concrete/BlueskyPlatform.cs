using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Bluesky;

/// <summary>Bluesky: handle + app password (no developer app); posts are records in the user's repository on the PDS.</summary>
public sealed class BlueskyPlatform : PlatformBase
{
    /// <summary>Creates.</summary>
    public BlueskyPlatform(HttpTransport transport, TimeProvider clock, ILoggerFactory loggers)
    {
        ArgumentNullException.ThrowIfNull(loggers);
        var sessions = new BlueskySessions(transport, clock);
        Connection = new BlueskyConnection(transport, clock, sessions);
        Refresher = sessions;
        Publisher = new BlueskyPublisher(transport, loggers.CreateLogger<BlueskyPublisher>());
    }

    /// <inheritdoc />
    public override string Code => PlatformCodes.Bluesky;

    /// <inheritdoc />
    public override string DisplayName => "Bluesky";

    /// <inheritdoc />
    public override string Color => "#1185FE";

    /// <inheritdoc />
    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Text, PostKind.Photo, PostKind.Video },
        MaxTextLength = 300,
        TextMeasure = GraphemeMeasure.Instance,
        MaxPhotos = 4,
        MaxPhotoBytes = 2L * 1024 * 1024,
        MaxVideoBytes = 50L * 1024 * 1024,
        Quotas = [new QuotaRule(1600, QuotaWindow.Rolling24Hours)],
        UnsupportedReasons = new Dictionary<PostKind, string> { [PostKind.Album] = "reason.no_mixed" },
    };

    /// <inheritdoc />
    public override IConnectionMethod Connection { get; }

    /// <inheritdoc />
    public override SetupGuide Guide => Steps(5, new Dictionary<int, Uri> { [1] = new("https://bsky.app/settings/app-passwords") }, null, "warning.bluesky.limits");

    /// <inheritdoc />
    public override IPublisher Publisher { get; }

    /// <inheritdoc />
    public override ITokenRefresher? Refresher { get; }
}
