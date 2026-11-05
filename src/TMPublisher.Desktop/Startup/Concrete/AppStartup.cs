using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Composition;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Logging;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Desktop.Views;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Desktop.Startup;

/// <summary>
/// Startup flow: first run (language → new start or restore) · unlock (automatic keyring, or master password) · main window + tray +
/// startup tasks. Closing the main window keeps the app running in the tray: the window is closed and released there and made again when
/// opened (<see cref="MainWindowHost"/>; it minimizes on Linux, where a tray host is not guaranteed). The app ends with Quit (tray menu or
/// the sidebar) or when the operating system shuts down or logs off.
/// </summary>
public sealed class AppStartup(IServiceProvider services, Vault vault, Localizer text, UiPreferencesFile preferences, AppSession session,
    WindowHost windows, MainWindowHost main, TrayHost tray, IOsModule os, ILogger<AppStartup> logger)
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private ShellViewModel? _shell;

    /// <summary>Runs the flow.</summary>
    public void Run(IClassicDesktopStyleApplicationLifetime desktop, bool startHidden)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        _desktop = desktop;
        Application.Current!.DataTemplates.Add(services.GetRequiredService<ViewLocator>());
        var language = preferences.Read().Language;
        text.Use(language.Length > 0 ? language : Localizer.SystemLanguage());
        switch (vault.State)
        {
            case VaultState.NotCreated:
                ShowFirstRun(language.Length == 0 ? FirstRunStep.Language : FirstRunStep.Choice);
                break;
            case VaultState.Locked when vault.Method is { } method && method != KeyMethods.Password:
                _ = OpenAutomaticAsync(startHidden);
                break;
            default:
                ShowUnlock(null);
                break;
        }
    }

    /// <summary>Shows and activates the main window, making it again if it was released to the tray (tray, second start).</summary>
    public void ShowMainWindow() => Dispatcher.UIThread.Post(() =>
    {
        if (_shell is not { } shell)
        {
            return;
        }

        main.Show();
        if (!shell.IsStarted)
        {
            // Started in the tray: the first page is opened (and its data read) only now.
            _ = shell.StartAsync();
        }
    });

    /// <summary>
    /// An exception that escaped a button or a window event (the UI dispatcher): logged with its cause and shown on the status line; the app
    /// and its scheduler keep running instead of ending.
    /// </summary>
    public void ReportUnhandled(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        logger.LogError(exception, "Unhandled exception in the window ({Type}); the app keeps running", exception.GetType().Name);
        if (_shell is not null)
        {
            _shell.Status = text.Format("error.unexpected", TMPublisher.Core.Security.Redactor.Redact(exception.Message));
        }
    }

    /// <summary>Ends the app.</summary>
    public void Quit()
    {
        main.BeginQuit();
        logger.LogInformation("Quit requested");
        _desktop?.Shutdown();
    }

    private void ShowFirstRun(FirstRunStep step)
    {
        var model = services.GetRequiredService<FirstRunViewModel>();
        model.Step = step;
        var window = new FirstRunWindow { DataContext = model };
        model.LanguageChosen += (_, _) =>
        {
            ShowFirstRun(FirstRunStep.Choice);
            window.Close();
        };
        model.Completed += async (_, _) =>
        {
            await StartMainAsync(startHidden: false).ConfigureAwait(true);
            window.Close();
        };
        Present(window);
    }

    private void ShowUnlock(string? error)
    {
        var model = services.GetRequiredService<UnlockViewModel>();
        model.Error = error ?? string.Empty;
        var window = new UnlockWindow { DataContext = model };
        model.Completed += async (_, _) =>
        {
            await StartMainAsync(startHidden: false).ConfigureAwait(true);
            window.Close();
        };
        model.RestoreRequested += (_, _) =>
        {
            ShowFirstRun(FirstRunStep.Restore);
            window.Close();
        };
        Present(window);
    }

    private void Present(Window window)
    {
        window.Icon = AppIcon();
        window.Closed += (_, _) =>
        {
            if (_shell is null && windows.Active == window)
            {
                // The first-run or unlock window was closed without finishing: nothing to run.
                Quit();
            }
        };
        windows.Active = window;
        _desktop!.MainWindow = window;
        window.Show();
    }

    private async Task OpenAutomaticAsync(bool startHidden)
    {
        try
        {
            await vault.OpenAsync(CancellationToken.None).ConfigureAwait(true);
            await StartMainAsync(startHidden).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The database could not be opened automatically");
            ShowUnlock(text.Format("unlock.automatic_failed", ex.Message));
        }
    }

    private async Task StartMainAsync(bool startHidden)
    {
        await session.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        tray.Create(ShowMainWindow, Quit);
        var shell = services.GetRequiredService<ShellViewModel>();
        _shell = shell;
        shell.QuitRequested += (_, _) => Quit();
        main.Configure(() => new MainWindow { DataContext = shell, Icon = AppIcon() }, () => os.TrayIsReliable && tray.HasIcon,
            window => _desktop!.MainWindow = window);
        if (!startHidden)
        {
            main.Show();
            await shell.StartAsync().ConfigureAwait(true);
        }

        foreach (var task in services.GetServices<IStartupTask>())
        {
            try
            {
                await task.StartAsync(CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Startup task {Task} failed", task.GetType().Name);
                shell.Status = text.Format("startup.task_failed", ex.Message);
            }
        }

        if (startHidden)
        {
            // Start-up garbage (database model, first reads) is not needed in the tray: one compacting collection, then the log line.
            MainWindowHost.Compact();
            MemoryUse.LogTrayEntry(logger, "started in the tray");
        }
    }

    private static WindowIcon AppIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://TMPublisher/Assets/app-256.png"));
        return new WindowIcon(stream);
    }
}
