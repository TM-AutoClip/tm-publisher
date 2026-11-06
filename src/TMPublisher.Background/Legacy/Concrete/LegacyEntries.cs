using TMPublisher.Runtime.Paths;

namespace TMPublisher.Background.Legacy;

/// <summary>Recognises an autostart command 0.1.0–0.1.4 wrote for a data folder.</summary>
public static class LegacyEntries
{
    /// <summary>
    /// Whether <paramref name="command"/> starts the 0.1.3–0.1.4 scheduler (<c>TMPublisherScheduler</c> / <c>tm-publisher-scheduler</c>) or the
    /// 0.1.0–0.1.2 window in the tray (<c>--tray</c>) for the data folder of <paramref name="paths"/>: no <c>--data-dir</c> for the default
    /// folder, that folder after <c>--data-dir</c> otherwise.
    /// </summary>
    public static bool IsLeftoverOf(AppPaths paths, string command)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(command);
        var old = command.Contains("TMPublisherScheduler", StringComparison.OrdinalIgnoreCase)
            || command.Contains("tm-publisher-scheduler", StringComparison.Ordinal)
            || command.Contains("--tray", StringComparison.Ordinal);
        if (!old)
        {
            return false;
        }

        var hasFolder = command.Contains("--data-dir", StringComparison.Ordinal);
        return paths.IsDefaultDirectory
            ? !hasFolder
            : hasFolder && command.Replace("\\\\", "\\", StringComparison.Ordinal)
                .Contains(paths.DataDirectory, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
