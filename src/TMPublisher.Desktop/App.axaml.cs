using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using TMPublisher.Desktop.Notifications;
using TMPublisher.Desktop.Startup;

namespace TMPublisher.Desktop;

/// <summary>
/// The Avalonia application. Services are built before Avalonia starts (<see cref="Program"/>); the startup flow (first run, unlock, main
/// window) is in <see cref="AppStartup"/>. In the notification process only <see cref="Notify"/> runs. Without either (headless tests) only the
/// styles load.
/// </summary>
public sealed partial class App : Application
{
    /// <summary>Services of the window program (set by <see cref="Program"/>).</summary>
    public static IServiceProvider? Services { get; set; }

    /// <summary>Set in the notification process (<c>--notify</c>).</summary>
    public static NotifyMode? Notify { get; set; }

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Notify is { } notify)
            {
                notify.Start(desktop);
            }
            else if (Services is { } services)
            {
                // The window program ends when its main window closes (AppStartup.Quit); first-run and unlock windows end it the same way.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                services.GetRequiredService<AppStartup>().Run(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
