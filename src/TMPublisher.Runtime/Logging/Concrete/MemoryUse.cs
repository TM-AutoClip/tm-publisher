using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Runtime.Logging;

/// <summary>
/// The memory the process uses, written to the log at quiet moments (and the peak of a run, the number to watch now that nothing runs between posts).
/// Windows: working set = what the system counts as in RAM now, private = memory only this process owns. Linux: RSS from
/// <c>/proc/self/status</c> with its anonymous (the process's own) and file-backed parts; .NET's "private" there is the virtual data size and is
/// not shown. GC heap = managed objects.
/// </summary>
public static class MemoryUse
{
    /// <summary>Writes one Information line.</summary>
    public static void Log(ILogger logger, string reason)
    {
        var gcHeap = Mb(GC.GetTotalMemory(forceFullCollection: false));
        if (OperatingSystem.IsLinux() && Status() is { } status)
        {
            logger.LogInformation("Memory ({Reason}): RSS {RssMb:F1} MB (anonymous {AnonMb:F1} MB, files {FileMb:F1} MB), GC heap {GcHeapMb:F1} MB", reason,
                status.GetValueOrDefault("VmRSS") / 1024.0, status.GetValueOrDefault("RssAnon") / 1024.0, status.GetValueOrDefault("RssFile") / 1024.0, gcHeap);
            return;
        }

        using var process = Process.GetCurrentProcess();
        logger.LogInformation("Memory ({Reason}): working set {WorkingSetMb:F1} MB, private {PrivateMb:F1} MB, GC heap {GcHeapMb:F1} MB", reason,
            Mb(process.WorkingSet64), Mb(process.PrivateMemorySize64), gcHeap);
    }

    /// <summary>
    /// The most the process held in RAM at once (Windows: peak working set; Linux: <c>VmHWM</c>, the peak RSS), in MB; <c>null</c> when the
    /// system does not tell.
    /// </summary>
    public static double? PeakMb()
    {
        if (OperatingSystem.IsLinux())
        {
            return Status() is { } status && status.TryGetValue("VmHWM", out var peak) ? peak / 1024.0 : null;
        }

        using var process = Process.GetCurrentProcess();
        return Mb(process.PeakWorkingSet64);
    }

    private static double Mb(long bytes) => bytes / (1024.0 * 1024.0);

    /// <summary>The kB values of <c>/proc/self/status</c>; <c>null</c> when it cannot be read.</summary>
    private static Dictionary<string, long>? Status()
    {
        try
        {
            var values = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var line in File.ReadLines("/proc/self/status"))
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2 && parts[1].Trim().Split(' ')[0] is { Length: > 0 } number
                    && long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kilobytes))
                {
                    values[parts[0]] = kilobytes;
                }
            }

            return values;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
