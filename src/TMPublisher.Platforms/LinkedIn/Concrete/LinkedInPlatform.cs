using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.LinkedIn;

/// <summary>
/// LinkedIn personal profile with the user's own LinkedIn app ("Share on LinkedIn" + "Sign In with LinkedIn using OpenID Connect"). The
/// token comes from LinkedIn's token generator; it lasts 60 days and only selected partners may refresh programmatically, so the user
/// connects again every 60 days (the account list warns a week before).
/// </summary>
public sealed class LinkedInPlatform(HttpTransport transport, TimeProvider clock, ILoggerFactory loggers) : PlatformBase
{
    /// <inheritdoc />
    public override string Code => PlatformCodes.LinkedIn;

    /// <inheritdoc />
    public override string DisplayName => "LinkedIn";

    /// <inheritdoc />
    public override string Color => "#0A66C2";

    /// <inheritdoc />
    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Text, PostKind.Photo, PostKind.Video },
        MaxTextLength = 3000,
        TextMeasure = Utf16Measure.Instance,
        MaxPhotos = 20,
        MaxVideoBytes = 500L * 1024 * 1024,
        Quotas = [new QuotaRule(150, QuotaWindow.Rolling24Hours)],
        UnsupportedReasons = new Dictionary<PostKind, string> { [PostKind.Album] = "reason.no_mixed" },
    };

    /// <inheritdoc />
    public override IConnectionMethod Connection { get; } = new LinkedInConnection(transport, clock);

    /// <inheritdoc />
    public override SetupGuide Guide => Steps(5,
        new Dictionary<int, Uri> { [1] = new("https://www.linkedin.com/developers/apps/new"), [3] = new("https://www.linkedin.com/developers/tools/oauth/token-generator") },
        null, "warning.linkedin.renew");

    /// <inheritdoc />
    public override IPublisher Publisher { get; } = new LinkedInPublisher(transport, loggers.CreateLogger<LinkedInPublisher>());
}
