using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Meta;

/// <summary>Facebook Page: text, photos (from this computer) and video, with the user's own Meta app.</summary>
public sealed class FacebookPlatform(HttpTransport transport, TimeProvider clock, ILoggerFactory loggers) : PlatformBase
{
    /// <inheritdoc />
    public override string Code => PlatformCodes.Facebook;

    /// <inheritdoc />
    public override string DisplayName => "Facebook";

    /// <inheritdoc />
    public override string Color => "#1877F2";

    /// <inheritdoc />
    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Text, PostKind.Photo, PostKind.Video },
        MaxTextLength = 63206,
        TextMeasure = Utf16Measure.Instance,
        MaxPhotos = 10,
        MaxPhotoBytes = 10L * 1024 * 1024,
        MaxVideoBytes = 1024L * 1024 * 1024,
        UnsupportedReasons = new Dictionary<PostKind, string> { [PostKind.Album] = "reason.no_mixed" },
    };

    /// <inheritdoc />
    public override IConnectionMethod Connection { get; } = new FacebookConnection(transport, clock);

    /// <inheritdoc />
    public override SetupGuide Guide => Steps(5,
        new Dictionary<int, Uri> { [1] = new("https://developers.facebook.com/apps/creation/"), [3] = new("https://developers.facebook.com/tools/explorer/") },
        null, "warning.meta.devmode");

    /// <inheritdoc />
    public override IPublisher Publisher { get; } = new FacebookPublisher(transport, loggers.CreateLogger<FacebookPublisher>());
}
