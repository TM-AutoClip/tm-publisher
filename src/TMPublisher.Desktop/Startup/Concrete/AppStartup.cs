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
using TMPublisher.Storage.Keys;

namespace TMPublisher.Desktop.Startup;

/// <summary>
/// Startup flow: first run (language → new start or restore) · unlock (automatic keyring, or master password) · main window + tray +
/// startup tasks. Closing the main window keeps the app running in the tray (minimizes on Linux, where a tray host is not guaranteed).
/// </summary>
public sealed class AppStartup(IServiceProvider services, Vault vault, Localizer text, UiPreferencesFile preferences, AppSession session,
    WindowHost windows, TrayHost tray, IOsModule os, ILogger<AppStartup> logger)
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private Window? _main;
    private bool _quitting;

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

    /// <summary>Shows and activates the main window (tray, second start).</summary>
    public void ShowMainWindow() => Dispatcher.UIThread.Post(() =>
    {
        if (_main is null)
        {
            return;
        }

        _main.Show();
        if (_main.WindowState == WindowState.Minimized)
        {
            _main.WindowState = WindowState.Normal;
        }

        _main.Activate();
    });

    /// <summary>Ends the app.</summary>
    public void Quit()
    {
        _quitting = true;
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
            if (_main is null && windows.Active == window)
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
        var window = new MainWindow { DataContext = shell, Icon = AppIcon() };
        window.Closing += (_, e) =>
        {
            if (_quitting)
            {
                return;
            }

            e.Cancel = true;
            if (os.TrayIsReliable && tray.HasIcon)
            {
                window.Hide();
            }
            else
            {
                window.WindowState = WindowState.Minimized;
            }
        };
        _main = window;
        windows.Active = window;
        _desktop!.MainWindow = window;
        if (!startHidden)
        {
            window.Show();
        }

        await shell.StartAsync().ConfigureAwait(true);
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

    private static WindowIcon AppIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://TMPublisher/Assets/app-256.png"));
        return new WindowIcon(stream);
    }
}
