using TMPublisher.Runtime.Os;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// What an autostart entry starts: the background scheduler next to the window program, with <c>--data-dir &lt;folder&gt;</c> when this run
/// uses another data folder than the default one. Without it a sign-in would open the empty default folder and the real schedule would never
/// run.
/// </summary>
public static class AutostartArguments
{
    /// <summary>The arguments for the data folder of <paramref name="paths"/>.</summary>
    public static IReadOnlyList<string> For(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.IsDefaultDirectory ? [] : ["--data-dir", paths.DataDirectory];
    }

    /// <summary>
    /// Whether an existing autostart command is the one 0.1.0–0.1.2 wrote for this data folder (the window program with <c>--tray</c>): only
    /// such an entry is moved to the scheduler. An entry of another data folder, or one the user made, is left alone.
    /// </summary>
    public static bool IsOldEntryOf(AppPaths paths, string command)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Contains("--tray", StringComparison.Ordinal))
        {
            return false;
        }

        var hasFolder = command.Contains("--data-dir", StringComparison.Ordinal);
        return paths.IsDefaultDirectory
            ? !hasFolder
            : hasFolder && command.Contains(paths.DataDirectory, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>The scheduler program; an exception when it is not next to the window program.</summary>
    public static string Program() =>
        ProgramFiles.Scheduler ?? throw new FileNotFoundException("The background scheduler (TMPublisherScheduler / tm-publisher-scheduler) is not next to the TM Publisher program.");
}
