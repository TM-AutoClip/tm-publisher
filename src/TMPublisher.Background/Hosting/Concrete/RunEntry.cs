using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMPublisher.Background.Composition;
using TMPublisher.Runtime.Logging;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Background.Hosting;

/// <summary>
/// The program started with <c>--run-due [--data-dir &lt;folder&gt;]</c> (by the system scheduler, or by hand): builds the run's services, does
/// one <see cref="DueRun"/> and ends. Nothing here touches a UI library, so none is loaded; the run's last log line says so, with how long it
/// took and the most memory it held. Exit code 0, or 1 when the run failed (the reason is in <c>logs/run-yyyy-MM-dd.log</c>).
/// </summary>
public static class RunEntry
{
    /// <summary>Runs; returns the exit code.</summary>
    public static int Run(string? dataDirectory)
    {
        var watch = Stopwatch.StartNew();
        var paths = new AppPaths(dataDirectory);
        using var services = BackgroundServices.Build(paths);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RunEntry));
        CrashLogging.Attach(logger);
        logger.LogInformation("TM Publisher run {Version} starting{Folder}", LogHeader.Version, paths.IsDefaultDirectory ? string.Empty : " (own data folder)");
        RunOutcome outcome;
        try
        {
            outcome = services.GetRequiredService<DueRun>().RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "The run stopped with an unhandled exception");
            outcome = RunOutcome.Failed;
        }

        logger.LogInformation("Run ended ({Outcome}) in {Seconds} s; peak memory {PeakMb} MB; UI library loaded: {Ui}", outcome,
            watch.Elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture),
            MemoryUse.PeakMb() is { } peak ? peak.ToString("0.0", CultureInfo.InvariantCulture) : "?", UiLoaded() ? "yes" : "no");
        return outcome == RunOutcome.Failed ? 1 : 0;
    }

    /// <summary>Whether an Avalonia assembly is in the process (it must not be in a run).</summary>
    public static bool UiLoaded() =>
        AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true);
}
