using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run value (no administrator rights), with <c>--tray</c>. Never rewritten at every start:
/// a user who turned it off in Task Manager keeps that choice.
/// </summary>
public sealed class WindowsAutostart(ILogger<WindowsAutostart> logger) : IAutostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TM Publisher";

    /// <inheritdoc />
    public bool IsEnabled
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    /// <inheritdoc />
    public void Set(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows autostart is only available on Windows.");
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, "\"" + Environment.ProcessPath + "\" --tray");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        logger.LogInformation("Autostart {State}", enabled ? "enabled" : "disabled");
    }
}
