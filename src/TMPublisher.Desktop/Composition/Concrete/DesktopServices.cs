using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Background.Composition;
using TMPublisher.Desktop.Accounts;
using TMPublisher.Platforms.Bluesky;
using TMPublisher.Platforms.Mastodon;
using TMPublisher.Platforms.X;
using TMPublisher.Platforms.YouTube;
using TMPublisher.Scheduling;
using TMPublisher.Core.Localization;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Settings;
using TMPublisher.Core.Stats;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Notifications;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.Sending;
using TMPublisher.Runtime.Logging;
using TMPublisher.Runtime.Paths;
using TMPublisher.Desktop.Startup;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Desktop.Views;
using TMPublisher.Storage.Backup;
using TMPublisher.Storage.Crypto;
using TMPublisher.Storage.Keys;
using TMPublisher.Storage.Stores;

namespace TMPublisher.Desktop.Composition;

/// <summary>The composition root of the window: the sending services (<see cref="SendingServices"/>, the same as a run's) and the window's own.</summary>
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
        SendingServices.Add(services, paths);
        services.AddSingleton<UiPreferencesFile>();
        services.AddSingleton<Localizer>();
        services.AddSingleton<ILocalizer>(s => s.GetRequiredService<Localizer>());
        services.AddSingleton(CurrentOs);

        // Storage of the window: the master password (the window can ask for it), backup, groups, tag sets.
        services.AddSingleton(KdfParameters.Default);
        services.AddSingleton<UiMasterPassword>();
        services.AddSingleton<IMasterPasswordSource>(s => s.GetRequiredService<UiMasterPassword>());
        services.AddSingleton<IKeyProtector, PasswordKeyProtector>();
        CurrentOs.Register(services);
        services.AddSingleton<BackupService>();
        services.AddSingleton<IGroupStore, GroupStore>();
        services.AddSingleton<ITagSetStore, TagSetStore>();

        // Desktop.
        services.AddSingleton<AppSession>();
        services.AddSingleton<BrowserLauncher>();
        services.AddSingleton<IUrlOpener>(s => s.GetRequiredService<BrowserLauncher>());
        services.AddTransient<ReportProblemViewModel>();
        services.AddSingleton<Func<ReportProblemViewModel>>(s => s.GetRequiredService<ReportProblemViewModel>);
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

        // Statistics (only the window reads them).
        services.AddSingleton<StatsService>();
        services.AddSingleton<IStatsReader, YouTubeStatsReader>();
        services.AddSingleton<IStatsReader, XStatsReader>();
        services.AddSingleton<IStatsReader, BlueskyStatsReader>();
        services.AddSingleton<IStatsReader, MastodonStatsReader>();

        // While it is open the window sends; it stores the user's decisions and sets the system scheduler's next start for when it is closed.
        services.AddSingleton<PostDecisions>();
        services.AddSingleton<WindowSender>();
        services.AddSingleton<IStartupTask>(s => s.GetRequiredService<WindowSender>());
        services.AddSingleton<IPostScheduler, WindowScheduler>();
        overrides?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    /// <summary>
    /// Registers a page: one instance, created the first time it is opened (not at start-up), and its sidebar
    /// entry, which needs only the page's static title, glyph and position.
    /// </summary>
    private static void AddPage<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(IServiceCollection services)
        where TPage : PageViewModelBase, IPageInfo
    {
        services.AddSingleton<TPage>();
        services.AddSingleton(s => new PageEntry(typeof(TPage), TPage.TitleKey, TPage.Glyph, TPage.Order, s.GetRequiredService<TPage>));
    }
}
