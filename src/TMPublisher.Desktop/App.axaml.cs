using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using TMPublisher.Desktop.Startup;

namespace TMPublisher.Desktop;

/// <summary>
/// The Avalonia application. Services are built before Avalonia starts (<see cref="Program"/>); the startup flow (first run, unlock, main
/// window, tray) is in <see cref="AppStartup"/>. Without services (headless tests) only the styles load.
/// </summary>
public sealed partial class App : Application
{
    /// <summary>Services of the app (set by <see cref="Program"/>).</summary>
    public static IServiceProvider? Services { get; set; }

    /// <summary>Started by the operating system session (autostart): stay in the tray.</summary>
    public static bool StartHidden { get; set; }

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (Services is { } services && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The app lives in the tray: closing the window does not end it, only "Quit" does.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            services.GetRequiredService<AppStartup>().Run(desktop, StartHidden);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
