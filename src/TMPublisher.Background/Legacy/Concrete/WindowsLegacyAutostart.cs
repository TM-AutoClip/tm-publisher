using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Background.Legacy;

/// <summary>
/// Windows: the value <c>TM Publisher</c> under HKCU\Software\Microsoft\Windows\CurrentVersion\Run that 0.1.0–0.1.4 wrote (no administrator
/// rights). Removed only when it is a leftover of this data folder (<see cref="LegacyEntries"/>).
/// </summary>
public sealed class WindowsLegacyAutostart(AppPaths paths, ILogger<WindowsLegacyAutostart> logger) : ILegacyAutostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>The value name (the one 0.1.0–0.1.4 used; replaced in tests).</summary>
    internal string ValueName { get; set; } = "TM Publisher";

    /// <inheritdoc />
    public bool RemoveLeftover()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is not string command || !LegacyEntries.IsLeftoverOf(paths, command))
        {
            return false;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
        logger.LogInformation("The 0.1.0-0.1.4 autostart entry (Run key) was removed: the system scheduler starts TM Publisher only when a post is due");
        return true;
    }
}
