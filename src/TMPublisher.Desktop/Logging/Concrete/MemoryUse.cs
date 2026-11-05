using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Desktop.Logging;

/// <summary>
/// The memory the app uses, written to the log when it goes to the tray (the app runs there all day: this is the number to watch).
/// Working set = what the operating system counts as in RAM now; private = memory only this process owns; GC heap = managed objects.
/// </summary>
internal static class MemoryUse
{
    /// <summary>Writes one Information line.</summary>
    public static void LogTrayEntry(ILogger logger, string reason)
    {
        using var process = Process.GetCurrentProcess();
        logger.LogInformation("Entering tray mode ({Reason}): working set {WorkingSetMb:F1} MB, private {PrivateMb:F1} MB, GC heap {GcHeapMb:F1} MB, GC committed {GcCommittedMb:F1} MB",
            reason, Mb(process.WorkingSet64), Mb(process.PrivateMemorySize64), Mb(GC.GetTotalMemory(forceFullCollection: false)), Mb(GC.GetGCMemoryInfo().TotalCommittedBytes));
    }

    private static double Mb(long bytes) => bytes / (1024.0 * 1024.0);
}
