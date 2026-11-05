using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Composition;
using TMPublisher.Desktop.Instance;
using TMPublisher.Desktop.Paths;

namespace TMPublisher.Desktop;

/// <summary>Entry point of the desktop app.</summary>
internal static class Program
{
    /// <summary>
    /// Arguments: <c>--tray</c> (start hidden in the tray; used by autostart) and <c>--data-dir &lt;folder&gt;</c> (or the environment variable
    /// <c>TMPUBLISHER_DATA_DIR</c>). A second start with the same data folder asks the running one to show its window and exits.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        var options = StartOptions.Parse(args);
        var paths = new AppPaths(options.DataDirectory);
        using var instance = SingleInstance.Acquire(paths.DataDirectory);
        if (!instance.IsFirst)
        {
            instance.SignalFirst();
            return 0;
        }

        using var services = DesktopServices.Build(paths);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Program));
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception (terminating: {Terminating})", e.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, e) => logger.LogError(e.Exception, "Unobserved task exception");
        logger.LogInformation("TM Publisher {Version} starting on {Os}", typeof(Program).Assembly.GetName().Version, Environment.OSVersion.Platform);

        instance.ShowRequested += (_, _) => services.GetRequiredService<Startup.AppStartup>().ShowMainWindow();

        // A command that throws (browser launch, a database write) must not end the app and its scheduler: logged, shown, handled.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            services.GetRequiredService<Startup.AppStartup>().ReportUnhandled(e.Exception);
            e.Handled = true;
        };
        App.Services = services;
        App.StartHidden = options.Tray;
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "The app stopped with an unhandled exception");
            throw;
        }
    }

    /// <summary>Avalonia app builder (also used by the designer).</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
