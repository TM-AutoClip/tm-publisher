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
    /// Arguments: <c>--tray</c> (start hidden in the tray; used by autostart), <c>--data-dir &lt;folder&gt;</c> (or the environment variable
    /// <c>TMPUBLISHER_DATA_DIR</c>) and <c>--gpu</c> (draw with the graphics card). A second start with the same data folder asks the running
    /// one to show its window and exits.
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
            logger.LogInformation("Drawing with the {Renderer}", options.Gpu ? "graphics card (--gpu)" : "processor");
            return BuildAvaloniaApp(options.Gpu).StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "The app stopped with an unhandled exception");
            throw;
        }
    }

    /// <summary>Avalonia app builder (also used by the designer).</summary>
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(gpu: false);

    /// <summary>
    /// Avalonia app builder. The app draws with the processor (Skia software rendering) unless <paramref name="gpu"/>: the graphics card path
    /// loads the graphics driver into the process at start-up, also in the tray where nothing is drawn, and keeps it (measured on Windows
    /// with an NVIDIA card, tray idle: about 30 MB more working set and 50 MB more private memory all day). A few forms and lists draw
    /// quickly enough without it.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp(bool gpu)
    {
        var builder = AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
        return gpu
            ? builder
            : builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
                .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
    }
}
