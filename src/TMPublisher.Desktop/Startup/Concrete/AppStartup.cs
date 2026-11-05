using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Composition;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Desktop.Views;
using TMPublisher.Core.Instance;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Desktop.Startup;

/// <summary>
/// Startup flow of the window program: first run (language → new start or restore) · unlock (automatic keyring, or master password) · main
/// window + startup tasks (the background scheduler is started if needed and gets the key of a master-password database). Closing the main
/// window ends the window program; the background scheduler keeps sending posts.
/// </summary>
public sealed class AppStartup(IServiceProvider services, Vault vault, Localizer text, UiPreferencesFile preferences, AppSession session,
    WindowHost windows, MainWindowHost main, IAutostart autostart, IOldVersionCheck oldVersion, ILogger<AppStartup> logger)
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private ShellViewModel? _shell;

    /// <summary>Runs the flow.</summary>
    public void Run(IClassicDesktopStyleApplicationLifetime desktop)
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
                _ = OpenAutomaticAsync();
                break;
            default:
                ShowUnlock(null);
                break;
        }
    }

    /// <summary>Shows and activates the main window (a second start of the window program asks for this).</summary>
    public void ShowMainWindow() => Dispatcher.UIThread.Post(() =>
    {
        if (_shell is not null)
        {
            main.Show();
        }
        else
        {
            windows.Active?.Activate();
        }
    });

    /// <summary>
    /// An exception that escaped a button or a window event (the UI dispatcher): logged with its cause and shown on the status line; the
    /// window keeps running instead of ending.
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

    /// <summary>Ends the window program (the background scheduler keeps running).</summary>
    public void Quit()
    {
        main.BeginQuit();
        logger.LogInformation("Window program ending");
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
            await StartMainAsync().ConfigureAwait(true);
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
            await StartMainAsync().ConfigureAwait(true);
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

    private async Task OpenAutomaticAsync()
    {
        try
        {
            await vault.OpenAsync(CancellationToken.None).ConfigureAwait(true);
            await StartMainAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The database could not be opened automatically");
            ShowUnlock(text.Format("unlock.automatic_failed", ex.Message));
        }
    }

    private async Task StartMainAsync()
    {
        await session.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        var shell = services.GetRequiredService<ShellViewModel>();
        _shell = shell;
        shell.QuitRequested += (_, _) => Quit();
        main.Configure(() => new MainWindow { DataContext = shell, Icon = AppIcon() }, shell.ConfirmClose, window => _desktop!.MainWindow = window);
        main.Closed += (_, _) => Quit();
        main.Show();
        await shell.StartAsync().ConfigureAwait(true);
        RepairAutostart(shell);
        if (oldVersion.OldVersionRuns())
        {
            // The scheduler is not started next to it (SchedulerSupervisor checks again); a running one claims nothing while it runs.
            logger.LogError("An older TM Publisher (0.1.0-0.1.2) runs on the same data; the background scheduler does not start next to it");
            shell.Status = text["scheduler.old_version"];
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
    }

    private void RepairAutostart(ShellViewModel shell)
    {
        try
        {
            autostart.Repair();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            logger.LogError(ex, "The autostart entry could not be moved to the background scheduler");
            shell.Status = text.Format("startup.task_failed", ex.Message);
        }
    }

    private static WindowIcon AppIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://TMPublisher/Assets/app-256.png"));
        return new WindowIcon(stream);
    }
}
