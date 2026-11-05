using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Threads;

/// <summary>
/// Threads with the user's own Meta app. Text only in this version: Threads takes photos and videos only from a public web address
/// (<c>image_url</c> / <c>video_url</c>), which a desktop app without a server does not have.
/// </summary>
public sealed class ThreadsPlatform : PlatformBase
{
    /// <summary>Creates.</summary>
    public ThreadsPlatform(HttpTransport transport, TimeProvider clock, ILoggerFactory loggers)
    {
        ArgumentNullException.ThrowIfNull(loggers);
        var refresher = new ThreadsTokenRefresher(transport, clock);
        Refresher = refresher;
        Connection = new ThreadsConnection(transport, clock, refresher);
        Publisher = new ThreadsPublisher(transport, loggers.CreateLogger<ThreadsPublisher>());
    }

    /// <inheritdoc />
    public override string Code => PlatformCodes.Threads;

    /// <inheritdoc />
    public override string DisplayName => "Threads";

    /// <inheritdoc />
    public override string Color => "#101010";

    /// <inheritdoc />
    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Text },
        MaxTextLength = 500,
        TextMeasure = Utf16Measure.Instance,
        Quotas = [new QuotaRule(250, QuotaWindow.Rolling24Hours)],
        UnsupportedReasons = new Dictionary<PostKind, string>
        {
            [PostKind.Photo] = "reason.threads_text_only",
            [PostKind.Video] = "reason.threads_text_only",
            [PostKind.Album] = "reason.threads_text_only",
        },
    };

    /// <inheritdoc />
    public override IConnectionMethod Connection { get; }

    /// <inheritdoc />
    public override SetupGuide Guide => Steps(6,
        new Dictionary<int, Uri> { [1] = new("https://developers.facebook.com/apps/creation/") },
        new Dictionary<int, string> { [3] = Core.Accounts.CredentialKeys.RedirectUri }, "warning.threads.text_only", "warning.threads.renewal");

    /// <inheritdoc />
    public override IPublisher Publisher { get; }

    /// <inheritdoc />
    public override ITokenRefresher? Refresher { get; }
}
