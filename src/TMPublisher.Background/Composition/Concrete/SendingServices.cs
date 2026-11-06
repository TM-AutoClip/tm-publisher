using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Background.Legacy;
using TMPublisher.Background.Os;
using TMPublisher.Background.Sending;
using TMPublisher.Background.Triggers;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Instance;
using TMPublisher.Core.Paths;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Quotas;
using TMPublisher.Core.Settings;
using TMPublisher.Platforms.Bluesky;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;
using TMPublisher.Platforms.Discord;
using TMPublisher.Platforms.LinkedIn;
using TMPublisher.Platforms.Mastodon;
using TMPublisher.Platforms.Meta;
using TMPublisher.Platforms.Pinterest;
using TMPublisher.Platforms.Telegram;
using TMPublisher.Platforms.Threads;
using TMPublisher.Platforms.TikTok;
using TMPublisher.Platforms.X;
using TMPublisher.Platforms.YouTube;
using TMPublisher.Runtime.Instance;
using TMPublisher.Runtime.Paths;
using TMPublisher.Scheduling;
using TMPublisher.Scheduling.Accounts;
using TMPublisher.Storage.Database;
using TMPublisher.Storage.Keys;
using TMPublisher.Storage.Stores;

namespace TMPublisher.Background.Composition;

/// <summary>
/// What sending needs, the same for the window and for a run the system scheduler started: the encrypted database and its stores, the
/// automatic key protector of this system, the platforms, the sender and the schedule, the sender lock, the system scheduler entry and the
/// clean-up of 0.1.0–0.1.4 leftovers. Each composition root adds its own logging, texts and notifications.
/// </summary>
public static class SendingServices
{
    private static readonly IBackgroundOs[] OsModules = [new WindowsBackgroundOs(), new LinuxBackgroundOs()];

    /// <summary>The operating system module of this computer.</summary>
    public static IBackgroundOs CurrentOs { get; } =
        OsModules.FirstOrDefault(m => m.IsCurrent) ?? throw new PlatformNotSupportedException("TM Publisher runs on Windows and Linux.");

    /// <summary>Registers the services.</summary>
    public static IServiceCollection Add(IServiceCollection services, AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(paths);
        services.AddSingleton<IAppPaths>(paths);
        CurrentOs.Register(services);

        // An old TM Publisher (0.1.0-0.1.2) on the same data sends without claiming: nothing is claimed next to it.
        services.AddSingleton<OldVersionGuard>();
        services.AddSingleton<IOldVersionCheck>(s => s.GetRequiredService<OldVersionGuard>());

        // Storage: the encrypted database with the stores sending needs.
        services.AddSingleton<DatabaseKey>();
        services.AddSingleton<Vault>();
        services.AddSingleton<IConnectionFactory, ConnectionFactory>();
        services.AddSingleton<IAccountStore, AccountStore>();
        services.AddSingleton<ICredentialStore, CredentialStore>();
        services.AddSingleton<IPostStore, PostStore>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<IPublishLog, PublishLogStore>();

        // Platforms and sending.
        services.AddSingleton(s => HttpTransport.Network.WithLog(s.GetRequiredService<ILoggerFactory>().CreateLogger("TMPublisher.Http")));
        services.AddSingleton<ILoopbackListener, LoopbackListener>();
        services.AddSingleton<IPlatformDirectory, PlatformDirectory>();
        services.AddSingleton<IRefreshGate, FileRefreshGate>();
        services.AddSingleton<CredentialRenewer>();
        services.AddSingleton<QuotaChecker>();
        services.AddSingleton<PostSender>();
        services.AddSingleton<ProcessingWatcher>();
        services.AddSingleton<PostScheduler>();
        services.AddSingleton<IPlatform, YouTubePlatform>();
        services.AddSingleton<IPlatform, XPlatform>();
        services.AddSingleton<IPlatform, TikTokPlatform>();
        services.AddSingleton<IPlatform, InstagramPlatform>();
        services.AddSingleton<IPlatform, FacebookPlatform>();
        services.AddSingleton<IPlatform, ThreadsPlatform>();
        services.AddSingleton<IPlatform, TelegramPlatform>();
        services.AddSingleton<IPlatform, DiscordPlatform>();
        services.AddSingleton<IPlatform, BlueskyPlatform>();
        services.AddSingleton<IPlatform, MastodonPlatform>();
        services.AddSingleton<IPlatform, PinterestPlatform>();
        services.AddSingleton<IPlatform, LinkedInPlatform>();

        // One sender at a time, the system scheduler's next start, the pause switch, 0.1.0-0.1.4 leftovers.
        services.AddSingleton<SenderGate>();
        services.AddSingleton<PauseSwitch>();
        services.AddSingleton<TriggerArmer>();
        services.AddSingleton<LegacyCleanup>();
        return services;
    }
}
