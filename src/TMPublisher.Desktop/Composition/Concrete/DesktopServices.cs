using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Desktop.Accounts;
using TMPublisher.Platforms.Bluesky;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Discord;
using TMPublisher.Platforms.LinkedIn;
using TMPublisher.Platforms.Mastodon;
using TMPublisher.Platforms.Pinterest;
using TMPublisher.Platforms.Meta;
using TMPublisher.Platforms.Telegram;
using TMPublisher.Platforms.Threads;
using TMPublisher.Platforms.TikTok;
using TMPublisher.Platforms.X;
using TMPublisher.Platforms.YouTube;
using TMPublisher.Platforms.Connections;
using TMPublisher.Scheduling;
using TMPublisher.Scheduling.Accounts;
using TMPublisher.Core.Instance;
using TMPublisher.Core.Localization;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Paths;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Quotas;
using TMPublisher.Core.Settings;
using TMPublisher.Core.Stats;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Notifications;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.Scheduler;
using TMPublisher.Runtime.Instance;
using TMPublisher.Runtime.Logging;
using TMPublisher.Runtime.Paths;
using TMPublisher.Desktop.Startup;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Desktop.Views;
using TMPublisher.Storage.Backup;
using TMPublisher.Storage.Crypto;
using TMPublisher.Storage.Database;
using TMPublisher.Storage.Keys;
using TMPublisher.Storage.Stores;

namespace TMPublisher.Desktop.Composition;

/// <summary>The composition root: every service of the app.</summary>
public static class DesktopServices
{
    private static readonly IOsModule[] OsModules = [new WindowsOsModule(), new LinuxOsModule()];

    /// <summary>The operating system module of this computer.</summary>
    public static IOsModule CurrentOs { get; } =
        OsModules.FirstOrDefault(m => m.IsCurrent) ?? throw new PlatformNotSupportedException("TM Publisher runs on Windows and Linux.");

    /// <summary>Builds the service provider.</summary>
    public static ServiceProvider Build(AppPaths paths, Action<IServiceCollection>? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var services = new ServiceCollection();
        // The container makes (and on dispose closes) the log file.
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<ILoggerProvider>(_ => new FileLoggerProvider(paths.LogDirectory, LogKinds.App));
        services.AddSingleton(new DrawingMode(Gpu: false));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(paths);

        // An old TM Publisher (0.1.0-0.1.2) on the same data: the scheduler neither starts nor claims next to it.
        services.AddSingleton<OldVersionGuard>();
        services.AddSingleton<IOldVersionCheck>(s => s.GetRequiredService<OldVersionGuard>());
        services.AddSingleton<IAppPaths>(paths);
        services.AddSingleton<UiPreferencesFile>();
        services.AddSingleton<Localizer>();
        services.AddSingleton<ILocalizer>(s => s.GetRequiredService<Localizer>());
        services.AddSingleton(CurrentOs);

        // Storage: encrypted database, key protectors, backup.
        services.AddSingleton<DatabaseKey>();
        services.AddSingleton(KdfParameters.Default);
        services.AddSingleton<UiMasterPassword>();
        services.AddSingleton<IMasterPasswordSource>(s => s.GetRequiredService<UiMasterPassword>());
        services.AddSingleton<IKeyProtector, PasswordKeyProtector>();
        CurrentOs.Register(services);
        services.AddSingleton<Vault>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<IConnectionFactory, ConnectionFactory>();
        services.AddSingleton<IAccountStore, AccountStore>();
        services.AddSingleton<ICredentialStore, CredentialStore>();
        services.AddSingleton<IGroupStore, GroupStore>();
        services.AddSingleton<IPostStore, PostStore>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<ITagSetStore, TagSetStore>();
        services.AddSingleton<IPublishLog, PublishLogStore>();

        // Desktop.
        services.AddSingleton<AppSession>();
        services.AddSingleton<BrowserLauncher>();
        services.AddSingleton<WindowHost>();
        services.AddSingleton<MainWindowHost>();
        services.AddSingleton<FileDialogs>();
        services.AddSingleton<ToastStack>();
        services.AddSingleton<ToastNotifier>();
        services.AddSingleton<INotifier>(s => s.GetRequiredService<ToastNotifier>());
        services.AddSingleton<AppStartup>();
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<FirstRunViewModel>();
        services.AddTransient<UnlockViewModel>();
        services.AddSingleton<AccountService>();
        services.AddTransient<ConnectViewModel>();
        services.AddSingleton<Func<ConnectViewModel>>(s => s.GetRequiredService<ConnectViewModel>);
        services.AddSingleton<PageNavigator>();
        AddPage<ComposeViewModel>(services);
        AddPage<CalendarViewModel>(services);
        AddPage<HistoryViewModel>(services);
        AddPage<AttentionViewModel>(services);
        AddPage<AccountsViewModel>(services);
        AddPage<GroupsViewModel>(services);
        AddPage<TagSetsViewModel>(services);
        AddPage<SettingsViewModel>(services);
        services.AddSingleton(new ViewLocator()
            .Register<ComposeViewModel, ComposeView>()
            .Register<CalendarViewModel, CalendarView>()
            .Register<HistoryViewModel, HistoryView>()
            .Register<AttentionViewModel, AttentionView>()
            .Register<AccountsViewModel, AccountsView>()
            .Register<TagSetsViewModel, TagSetsView>()
            .Register<GroupsViewModel, GroupsView>()
            .Register<SettingsViewModel, SettingsView>());

        // Platforms (one class each) and the services around them.
        services.AddSingleton(s => HttpTransport.Network.WithLog(s.GetRequiredService<ILoggerFactory>().CreateLogger("TMPublisher.Http")));
        services.AddSingleton<ILoopbackListener, LoopbackListener>();
        services.AddSingleton<IPlatformDirectory, PlatformDirectory>();
        services.AddSingleton<IRefreshGate, FileRefreshGate>();
        services.AddSingleton<CredentialRenewer>();
        services.AddSingleton<QuotaChecker>();
        services.AddSingleton<StatsService>();
        services.AddSingleton<IStatsReader, YouTubeStatsReader>();
        services.AddSingleton<IStatsReader, XStatsReader>();
        services.AddSingleton<IStatsReader, BlueskyStatsReader>();
        services.AddSingleton<IStatsReader, MastodonStatsReader>();

        // The background scheduler (its own process) sends; the window stores decisions and talks to it.
        services.AddSingleton<PostDecisions>();
        services.AddSingleton<ISchedulerLauncher, ProcessSchedulerLauncher>();
        services.AddSingleton<SchedulerSupervisor>();
        services.AddSingleton<IStartupTask>(s => s.GetRequiredService<SchedulerSupervisor>());
        services.AddSingleton<IPostScheduler, SchedulerBridge>();
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
        overrides?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    /// <summary>
    /// Registers a page: one instance, created the first time it is opened (not at start-up; in the tray no page is made), and its sidebar
    /// entry, which needs only the page's static title, glyph and position.
    /// </summary>
    private static void AddPage<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(IServiceCollection services)
        where TPage : PageViewModelBase, IPageInfo
    {
        services.AddSingleton<TPage>();
        services.AddSingleton(s => new PageEntry(typeof(TPage), TPage.TitleKey, TPage.Glyph, TPage.Order, s.GetRequiredService<TPage>));
    }
}
