using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Background.Composition;
using TMPublisher.Background.Hosting;
using TMPublisher.Background.Ipc;
using TMPublisher.Background.Os;
using TMPublisher.Runtime.Instance;
using TMPublisher.Runtime.Logging;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Background;

/// <summary>Entry point of the background scheduler.</summary>
internal static class Program
{
    /// <summary>
    /// Arguments: <c>--data-dir &lt;folder&gt;</c> (or the environment variable <c>TMPUBLISHER_DATA_DIR</c>). Exactly one scheduler runs per
    /// data folder (<c>scheduler-instance.lock</c>): a second start ends at once. Exit codes: 0 normal end or already running, 2 an old TM Publisher
    /// (0.1.0–0.1.2, which sent posts from its own window) runs on the same data, 1 an error (in the log). On Windows the old version's
    /// single-instance name is kept open for the whole run, so a 0.1.0–0.1.2 started later ends at once; on Linux every claim looks for it again
    /// (<see cref="OldVersionGuard"/>).
    /// </summary>
    public static int Main(string[] args)
    {
        var paths = new AppPaths(DataDirectory(args));
        using var instance = InstanceLock.TryAcquire(paths.DataDirectory, InstanceLock.Scheduler, out var reason);
        if (instance is null)
        {
            // Nothing is logged to the file: the running scheduler owns it.
            Console.Error.WriteLine("The TM Publisher scheduler already runs for this data folder (" + reason + ").");
            return 0;
        }

        using var services = BackgroundServices.Build(paths);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Program));
        CrashLogging.Attach(logger);
        logger.LogInformation("TM Publisher scheduler {Version} starting{Folder}", LogHeader.Version, paths.IsDefaultDirectory ? string.Empty : " (own data folder)");
        var oldVersion = services.GetRequiredService<OldVersionGuard>();
        using var oldName = oldVersion.HoldName();
        if (oldVersion.OldVersionRuns())
        {
            logger.LogError("An older TM Publisher (0.1.0-0.1.2) is running on the same data and sends posts itself; quit it (tray: Quit) and start again. "
                + "Two schedulers could send a post twice, so this one does not start.");
            return 2;
        }

        try
        {
            var loop = services.GetRequiredService<IMainLoop>();
            var host = services.GetRequiredService<SchedulerHost>();
            services.GetRequiredService<ITrayIcon>();
            services.GetRequiredService<SchedulerServer>().Start();
            _ = Task.Run(host.StartAsync);
            loop.Run();
            logger.LogInformation("TM Publisher scheduler ended");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "The scheduler stopped with an unhandled exception");
            return 1;
        }
    }

    private static string? DataDirectory(string[] args)
    {
        string? data = null;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--data-dir")
            {
                data = args[i + 1];
            }
        }

        data ??= Environment.GetEnvironmentVariable("TMPUBLISHER_DATA_DIR");
        return string.IsNullOrWhiteSpace(data) ? null : Path.GetFullPath(data);
    }
}
