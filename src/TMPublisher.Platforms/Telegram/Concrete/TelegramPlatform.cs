using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Telegram;

/// <summary>Telegram: a bot of the user posts to the user's channel or group (bot token from BotFather; the bot is an admin there).</summary>
public sealed class TelegramPlatform(HttpTransport transport, TimeProvider clock, ILoggerFactory loggers) : PlatformBase
{
    /// <summary>Bot API files up to 50 MB (cloud Bot API server).</summary>
    public const long MaxFileBytes = 50L * 1024 * 1024;

    /// <inheritdoc />
    public override string Code => PlatformCodes.Telegram;

    /// <inheritdoc />
    public override string DisplayName => "Telegram";

    /// <inheritdoc />
    public override string Color => "#229ED9";

    /// <inheritdoc />
    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Text, PostKind.Photo, PostKind.Video, PostKind.Album },
        MaxTextLength = 4096,
        MaxMediaTextLength = 1024,
        TextMeasure = Utf16Measure.Instance,
        MaxPhotos = 10,
        MaxAlbumItems = 10,
        MaxPhotoBytes = 10L * 1024 * 1024,
        MaxVideoBytes = MaxFileBytes,
        Quotas = [new QuotaRule(20 * 60 * 24, QuotaWindow.Rolling24Hours)],
    };

    /// <inheritdoc />
    public override IConnectionMethod Connection { get; } = new TelegramConnection(transport, clock);

    /// <inheritdoc />
    public override SetupGuide Guide => Steps(5, new Dictionary<int, Uri> { [1] = new("https://t.me/BotFather") }, null, "warning.telegram.limits");

    /// <inheritdoc />
    public override IPublisher Publisher { get; } = new TelegramPublisher(transport, loggers.CreateLogger<TelegramPublisher>());
}
