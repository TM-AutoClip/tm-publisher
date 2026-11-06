using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Composition;
using TMPublisher.Desktop.Instance;
using TMPublisher.Desktop.Notifications;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.Sending;
using TMPublisher.Runtime.Logging;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Desktop.Startup;

/// <summary>
/// The program with a window: the window itself (one per data folder; a second start asks the open one to come to the front and ends), or
/// with <c>--notify</c> only the corner notifications a run writes to standard input.
/// </summary>
internal static class WindowProgram
{
    /// <summary>Runs; returns the exit code.</summary>
    public static int Run(StartOptions options, string[] args)
    {
        var paths = new AppPaths(options.DataDirectory);
        if (options.Notify)
        {
            return RunNotifications(paths, args, options.Gpu);
        }

        using var instance = SingleInstance.Acquire(paths.DataDirectory);
        if (!instance.IsFirst)
        {
            using var logFile = new FileLoggerProvider(paths.LogDirectory, LogKinds.App);
            using var loggers = LoggerFactory.Create(b => b.AddProvider(logFile).SetMinimumLevel(LogLevel.Information));
            instance.SignalFirst(loggers.CreateLogger(typeof(WindowProgram)));
            return 0;
        }

        using var services = DesktopServices.Build(paths, s => s.AddSingleton(new DrawingMode(options.Gpu)));
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(WindowProgram));
        CrashLogging.Attach(logger);
        logger.LogInformation("TM Publisher {Version} starting{Folder}", LogHeader.Version, paths.IsDefaultDirectory ? string.Empty : " (own data folder)");

        instance.Logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<SingleInstance>();
        instance.ShowRequested += (_, _) =>
        {
            logger.LogInformation("A second start asked for the window");
            services.GetRequiredService<AppStartup>().ShowMainWindow();
        };

        // A command that throws (browser launch, a database write) must not end the window: logged, shown, handled.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            services.GetRequiredService<AppStartup>().ReportUnhandled(e.Exception);
            e.Handled = true;
        };
        App.Services = services;
        try
        {
            logger.LogInformation("Drawing with the {Renderer}", options.Gpu ? "graphics card (--gpu)" : "processor");
            var code = BuildAvaloniaApp(options.Gpu).StartWithClassicDesktopLifetime(args);

            // The window is gone: the post being sent may finish, the system scheduler's next start is set, the sender lock is given back.
            services.GetRequiredService<WindowSender>().Close();
            logger.LogInformation("TM Publisher ended ({Code})", code);
            return code;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "The app stopped with an unhandled exception");
            throw;
        }
    }

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

    /// <summary>The short-lived notification process of a run: no database, no main window.</summary>
    private static int RunNotifications(AppPaths paths, string[] args, bool gpu)
    {
        using var logFile = new FileLoggerProvider(paths.LogDirectory, LogKinds.Notify);
        using var loggers = LoggerFactory.Create(b => b.AddProvider(logFile).SetMinimumLevel(LogLevel.Information));
        var logger = loggers.CreateLogger(typeof(WindowProgram));
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
}
