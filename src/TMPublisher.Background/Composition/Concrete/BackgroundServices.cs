using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Background.Hosting;
using TMPublisher.Background.Ipc;
using TMPublisher.Background.Localization;
using TMPublisher.Background.Notifications;
using TMPublisher.Background.Os;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Localization;
using TMPublisher.Core.Notifications;
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
using TMPublisher.Runtime.Logging;
using TMPublisher.Runtime.Paths;
using TMPublisher.Scheduling;
using TMPublisher.Scheduling.Accounts;
using TMPublisher.Storage.Database;
using TMPublisher.Storage.Keys;
using TMPublisher.Storage.Stores;

namespace TMPublisher.Background.Composition;

/// <summary>The scheduler's composition root: only what sending scheduled posts needs (no account connection screens, no statistics).</summary>
public static class BackgroundServices
{
    private static readonly IBackgroundOs[] OsModules = [new WindowsBackgroundOs(), new LinuxBackgroundOs()];

    /// <summary>Builds the service provider.</summary>
    public static ServiceProvider Build(AppPaths paths, Action<IServiceCollection>? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var os = OsModules.FirstOrDefault(m => m.IsCurrent) ?? throw new PlatformNotSupportedException("TM Publisher runs on Windows and Linux.");
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(new FileLoggerProvider(paths.LogDirectory, "scheduler")).SetMinimumLevel(LogLevel.Information));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(paths);
        services.AddSingleton<IAppPaths>(paths);
        services.AddSingleton<TextCatalog>();
        services.AddSingleton<ILocalizer>(s => s.GetRequiredService<TextCatalog>());

        // Storage: the same encrypted database as the window; no master-password protector (the window hands that key over).
        services.AddSingleton<DatabaseKey>();
        services.AddSingleton<Vault>();
        services.AddSingleton<IConnectionFactory, ConnectionFactory>();
        services.AddSingleton<IAccountStore, AccountStore>();
        services.AddSingleton<ICredentialStore, CredentialStore>();
        services.AddSingleton<IPostStore, PostStore>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<IPublishLog, PublishLogStore>();

        // Platforms and sending.
        services.AddSingleton(HttpTransport.Network);
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

        // The process around it.
        services.AddSingleton<WatcherHub>();
        services.AddSingleton<NotifyProcess>();
        services.AddSingleton<INotifier, NotificationRelay>();
        services.AddSingleton<SchedulerHost>();
        services.AddSingleton<SchedulerServer>();
        os.Register(services);
        overrides?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }
}
