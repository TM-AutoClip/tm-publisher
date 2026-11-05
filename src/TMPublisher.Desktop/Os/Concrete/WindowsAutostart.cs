using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TMPublisher.Runtime.Os;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run value (no administrator rights) that starts the background scheduler, with the data
/// folder when it is not the default (<see cref="AutostartArguments"/>). Only the command is ever repaired, never re-enabled: a user who turned
/// it off in Task Manager (a separate StartupApproved entry) keeps that choice.
/// </summary>
public sealed class WindowsAutostart(AppPaths paths, ILogger<WindowsAutostart> logger) : IAutostart
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
            key.SetValue(ValueName, Expected());
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        logger.LogInformation("Autostart {State}{Folder}", enabled ? "enabled" : "disabled", paths.IsDefaultDirectory ? string.Empty : " (own data folder)");
    }

    /// <inheritdoc />
    public void Repair()
    {
        if (!OperatingSystem.IsWindows() || ProgramFiles.Scheduler is null)
        {
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is string current && AutostartArguments.IsOldEntryOf(paths, current))
        {
            key.SetValue(ValueName, Expected());
            logger.LogInformation("Autostart entry moved to the background scheduler");
        }
    }

    private string Expected() => Command(AutostartArguments.Program(), AutostartArguments.For(paths));

    /// <summary>The Run value: every part quoted as Windows parses a command line (a trailing backslash is doubled before the quote).</summary>
    internal static string Command(string executable, IReadOnlyList<string> arguments) =>
        string.Join(' ', new[] { executable }.Concat(arguments).Select(Quote));

    private static string Quote(string part) =>
        part.StartsWith("--", StringComparison.Ordinal) && !part.Contains(' ', StringComparison.Ordinal)
            ? part
            : "\"" + (part.EndsWith('\\') ? part + "\\" : part) + "\"";
}
