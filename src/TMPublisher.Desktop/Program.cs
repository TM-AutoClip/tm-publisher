using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Composition;
using TMPublisher.Desktop.Instance;
using TMPublisher.Desktop.Notifications;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.Scheduler;
using TMPublisher.Runtime.Logging;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Desktop;

/// <summary>Entry point of the window program.</summary>
internal static class Program
{
    /// <summary>
    /// Arguments: <c>--data-dir &lt;folder&gt;</c> (or the environment variable <c>TMPUBLISHER_DATA_DIR</c>), <c>--gpu</c> (draw with the
    /// graphics card), <c>--notify</c> (only show the notifications the background scheduler writes to standard input, then end) and
    /// <c>--tray</c> (the autostart entry of 0.1.0–0.1.2: start the background scheduler, move the autostart entry to it, open no window). A
    /// second start with the same data folder asks the open window to come to the front and exits.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        var options = StartOptions.Parse(args);
        var paths = new AppPaths(options.DataDirectory);
        if (options.Notify)
        {
            return RunNotifications(paths, args, options.Gpu);
        }

        if (options.Tray)
        {
            return StartSchedulerOnly(paths);
        }

        using var instance = SingleInstance.Acquire(paths.DataDirectory);
        if (!instance.IsFirst)
        {
            using var logFile = new FileLoggerProvider(paths.LogDirectory, LogKinds.App);
            using var loggers = LoggerFactory.Create(b => b.AddProvider(logFile).SetMinimumLevel(LogLevel.Information));
            instance.SignalFirst(loggers.CreateLogger(typeof(Program)));
            return 0;
        }

        using var services = DesktopServices.Build(paths, s => s.AddSingleton(new DrawingMode(options.Gpu)));
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Program));
        CrashLogging.Attach(logger);
        logger.LogInformation("TM Publisher {Version} starting{Folder}", LogHeader.Version, paths.IsDefaultDirectory ? string.Empty : " (own data folder)");

        instance.Logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<SingleInstance>();
        instance.ShowRequested += (_, _) =>
        {
            logger.LogInformation("A second start asked for the window");
            services.GetRequiredService<Startup.AppStartup>().ShowMainWindow();
        };

        // A command that throws (browser launch, a database write) must not end the window: logged, shown, handled.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            services.GetRequiredService<Startup.AppStartup>().ReportUnhandled(e.Exception);
            e.Handled = true;
        };
        App.Services = services;
        try
        {
            logger.LogInformation("Drawing with the {Renderer}", options.Gpu ? "graphics card (--gpu)" : "processor");
            var code = BuildAvaloniaApp(options.Gpu).StartWithClassicDesktopLifetime(args);
            logger.LogInformation("TM Publisher ended ({Code})", code);
            return code;
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
    /// loads the graphics driver into the process at start-up (measured on Windows with an NVIDIA card: about 30 MB more working set and 50 MB
    /// more private memory). A few forms and lists draw quickly enough without it.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp(bool gpu)
    {
        var builder = AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
        return gpu
            ? builder
            : builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
                .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
    }

    /// <summary>The short-lived notification process of the background scheduler: no database, no main window.</summary>
    private static int RunNotifications(AppPaths paths, string[] args, bool gpu)
    {
        using var logFile = new FileLoggerProvider(paths.LogDirectory, LogKinds.Notify);
        using var loggers = LoggerFactory.Create(b => b.AddProvider(logFile).SetMinimumLevel(LogLevel.Information));
        var logger = loggers.CreateLogger(typeof(Program));
        CrashLogging.Attach(logger);
        logger.LogInformation("Notification process starting");
        try
        {
            App.Notify = new NotifyMode(new ToastStack(), loggers.CreateLogger<NotifyMode>());
            var code = BuildAvaloniaApp(gpu).StartWithClassicDesktopLifetime(args);
            logger.LogInformation("Notification process ended ({Code})", code);
            return code;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "The notification process stopped with an unhandled exception");
            return 1;
        }
    }

    /// <summary>
    /// <c>--tray</c>, the autostart entry of 0.1.0–0.1.2: the window program no longer waits in the tray. Starts the background scheduler,
    /// moves the autostart entry to it and ends without a window.
    /// </summary>
    private static int StartSchedulerOnly(AppPaths paths)
    {
        using var services = DesktopServices.Build(paths);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Program));
        logger.LogInformation("Started with --tray (an autostart entry of an older version): starting the background scheduler instead");
        try
        {
            services.GetRequiredService<IAutostart>().Repair();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            logger.LogError(ex, "The autostart entry could not be moved to the background scheduler");
        }

        if (!services.GetRequiredService<ISchedulerLauncher>().TryStart(out var reason))
        {
            logger.LogError("The background scheduler could not be started: {Reason}", reason);
            return 1;
        }

        logger.LogInformation("Background scheduler started; the window program ends (--tray)");
        return 0;
    }
}
