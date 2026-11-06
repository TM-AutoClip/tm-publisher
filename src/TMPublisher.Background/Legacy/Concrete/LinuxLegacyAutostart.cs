using Microsoft.Extensions.Logging;
using TMPublisher.Background.Triggers;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Background.Legacy;

/// <summary>
/// Linux: <c>$XDG_CONFIG_HOME/autostart/tm-publisher.desktop</c>, the autostart entry 0.1.0–0.1.4 wrote. Removed only when its <c>Exec=</c>
/// line is a leftover of this data folder (<see cref="LegacyEntries"/>).
/// </summary>
public sealed class LinuxLegacyAutostart(AppPaths paths, ILogger<LinuxLegacyAutostart> logger) : ILegacyAutostart
{
    /// <summary>The entry's file (replaced in tests).</summary>
    internal string File { get; set; } = Path.Combine(XdgAutostartEntry.ConfigFolder, "autostart", "tm-publisher.desktop");

    /// <inheritdoc />
    public bool RemoveLeftover()
    {
        if (!System.IO.File.Exists(File)
            || System.IO.File.ReadLines(File).FirstOrDefault(l => l.StartsWith("Exec=", StringComparison.Ordinal)) is not { } exec
            || !LegacyEntries.IsLeftoverOf(paths, exec))
        {
            return false;
        }

        System.IO.File.Delete(File);
        logger.LogInformation("The 0.1.0-0.1.4 autostart entry was removed: the system scheduler starts TM Publisher only when a post is due");
        return true;
    }
}
