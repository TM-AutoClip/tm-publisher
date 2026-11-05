using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Localization;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Paths;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Quotas;
using TMPublisher.Core.Settings;
using TMPublisher.Desktop.Logging;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Notifications;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Paths;
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
        services.AddLogging(b => b.AddProvider(new FileLoggerProvider(paths.LogDirectory)).SetMinimumLevel(LogLevel.Information));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(paths);
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
        services.AddSingleton<IDbContextFactory<PublisherDbContext>, PublisherDbContextFactory>();
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
        services.AddSingleton<FileDialogs>();
        services.AddSingleton<ToastNotifier>();
        services.AddSingleton<INotifier>(s => s.GetRequiredService<ToastNotifier>());
        services.AddSingleton<TrayHost>();
        services.AddSingleton<AppStartup>();
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<FirstRunViewModel>();
        services.AddTransient<UnlockViewModel>();
        services.AddSingleton<PageViewModelBase, SettingsViewModel>();
        services.AddSingleton(new ViewLocator()
            .Register<SettingsViewModel, SettingsView>());
        overrides?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }
}
