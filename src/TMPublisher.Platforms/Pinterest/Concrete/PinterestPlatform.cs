using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Pinterest;

/// <summary>
/// Pinterest with the user's own app: one image pin (sent as base64) or one video pin, to the board chosen when connecting. With Trial
/// access the pins are visible only to their creator; Standard access needs a short video demo of the app's sign-in for Pinterest's
/// review, even for one user.
/// </summary>
public sealed class PinterestPlatform : PlatformBase
{
    /// <summary>Creates.</summary>
    public PinterestPlatform(HttpTransport transport, TimeProvider clock, ILoggerFactory loggers)
    {
        ArgumentNullException.ThrowIfNull(loggers);
        Refresher = new PinterestTokenRefresher(transport, clock);
        Connection = new PinterestConnection(transport, clock);
        Publisher = new PinterestPublisher(transport, loggers.CreateLogger<PinterestPublisher>());
    }

    /// <inheritdoc />
    public override string Code => PlatformCodes.Pinterest;

    /// <inheritdoc />
    public override string DisplayName => "Pinterest";

    /// <inheritdoc />
    public override string Color => "#E60023";

    /// <inheritdoc />
    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Photo, PostKind.Video },
        HasTitle = true,
        MaxTitleLength = 100,
        MaxTextLength = 800,
        TextMeasure = Utf16Measure.Instance,
        MaxPhotos = 1,
        MaxPhotoBytes = 20L * 1024 * 1024,
        MaxVideoBytes = 2L * 1024 * 1024 * 1024,
        UnsupportedReasons = new Dictionary<PostKind, string>
        {
            [PostKind.Text] = "reason.pinterest_media",
            [PostKind.Album] = "reason.pinterest_media",
        },
    };

    /// <inheritdoc />
    public override IConnectionMethod Connection { get; }

    /// <inheritdoc />
    public override SetupGuide Guide => Steps(5,
        new Dictionary<int, Uri> { [1] = new("https://developers.pinterest.com/apps/") },
        new Dictionary<int, string> { [2] = Core.Accounts.CredentialKeys.RedirectUri }, "warning.pinterest.trial", "warning.pinterest.business");

    /// <inheritdoc />
    public override IPublisher Publisher { get; }

    /// <inheritdoc />
    public override ITokenRefresher? Refresher { get; }
}
