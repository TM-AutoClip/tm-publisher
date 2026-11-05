using System.ComponentModel;
using System.Diagnostics;
using TMPublisher.Runtime.Os;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Desktop.Scheduler;

/// <summary>
/// Starts <c>TMPublisherScheduler.exe</c> / <c>tm-publisher-scheduler</c> from the window program's folder, with the data folder when it is
/// not the default one. The scheduler keeps running when the window closes (on Linux it also ignores the terminal's SIGHUP).
/// </summary>
public sealed class ProcessSchedulerLauncher(AppPaths paths) : ISchedulerLauncher
{
    /// <inheritdoc />
    public bool TryStart(out string reason)
    {
        if (ProgramFiles.Scheduler is not { } scheduler)
        {
            reason = "the scheduler program was not found next to the window program";
            return false;
        }

        var start = new ProcessStartInfo(scheduler) { UseShellExecute = false };
        if (!paths.IsDefaultDirectory)
        {
            start.ArgumentList.Add("--data-dir");
            start.ArgumentList.Add(paths.DataDirectory);
        }

        try
        {
            using var process = Process.Start(start);
            reason = process is null ? "the system did not start it" : string.Empty;
            return process is not null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            reason = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }
}
