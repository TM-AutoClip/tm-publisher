using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Mastodon;

/// <summary>Mastodon: the app registers the user's own app on the user's server (no approval), then a pasted code.</summary>
public sealed class MastodonPlatform(HttpTransport transport, TimeProvider clock, ILoggerFactory loggers) : PlatformBase
{
    /// <inheritdoc />
    public override string Code => PlatformCodes.Mastodon;

    /// <inheritdoc />
    public override string DisplayName => "Mastodon";

    /// <inheritdoc />
    public override string Color => "#6364FF";

    /// <inheritdoc />
    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Text, PostKind.Photo, PostKind.Video },
        MaxTextLength = 500,
        TextMeasure = LinkAs23Measure.Instance,
        MaxPhotos = 4,
        MaxPhotoBytes = 16L * 1024 * 1024,
        MaxVideoBytes = 99L * 1024 * 1024,
        SupportsPrivacy = true,
        UnsupportedReasons = new Dictionary<PostKind, string> { [PostKind.Album] = "reason.no_mixed" },
    };

    /// <inheritdoc />
    public override IConnectionMethod Connection { get; } = new MastodonConnection(transport, clock);

    /// <inheritdoc />
    public override SetupGuide Guide => Steps(4, null, null, "warning.mastodon.limits");

    /// <inheritdoc />
    public override IPublisher Publisher { get; } = new MastodonPublisher(transport, loggers.CreateLogger<MastodonPublisher>());
}
