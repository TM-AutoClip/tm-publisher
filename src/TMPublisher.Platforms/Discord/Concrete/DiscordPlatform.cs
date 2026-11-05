using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Discord;

/// <summary>Discord: posts through a channel webhook (no bot needed).</summary>
public sealed class DiscordPlatform(HttpTransport transport, TimeProvider clock, ILoggerFactory loggers) : PlatformBase
{
    /// <summary>Largest file (20 MiB per file; the request may carry 25 MiB).</summary>
    public const long MaxFileBytes = 20L * 1024 * 1024;

    /// <summary>Largest request.</summary>
    public const long MaxRequestBytes = 25L * 1024 * 1024;

    /// <inheritdoc />
    public override string Code => PlatformCodes.Discord;

    /// <inheritdoc />
    public override string DisplayName => "Discord";

    /// <inheritdoc />
    public override string Color => "#5865F2";

    /// <inheritdoc />
    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Text, PostKind.Photo, PostKind.Video, PostKind.Album },
        MaxTextLength = 2000,
        TextMeasure = Utf16Measure.Instance,
        MaxPhotos = 10,
        MaxAlbumItems = 10,
        MaxPhotoBytes = MaxFileBytes,
        MaxVideoBytes = MaxFileBytes,
    };

    /// <inheritdoc />
    public override IConnectionMethod Connection { get; } = new DiscordConnection(transport, clock);

    /// <inheritdoc />
    public override SetupGuide Guide => Steps(4, new Dictionary<int, Uri> { [1] = new("https://support.discord.com/hc/articles/228383668") }, null,
        "warning.discord.secret", "warning.discord.limits");

    /// <inheritdoc />
    public override IPublisher Publisher { get; } = new DiscordPublisher(transport, loggers.CreateLogger<DiscordPublisher>());
}
