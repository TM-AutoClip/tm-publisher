using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TMPublisher.Desktop.Paths;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run value (no administrator rights), with <c>--tray</c> and the data folder when it is
/// not the default (<see cref="AutostartArguments"/>). Never rewritten at every start: a user who turned it off in Task Manager keeps that
/// choice.
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
            key.SetValue(ValueName, Command(Environment.ProcessPath ?? string.Empty, AutostartArguments.For(paths)));
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        logger.LogInformation("Autostart {State}{Folder}", enabled ? "enabled" : "disabled", paths.IsDefaultDirectory ? string.Empty : " (own data folder)");
    }

    /// <summary>The Run value: every part quoted as Windows parses a command line (a trailing backslash is doubled before the quote).</summary>
    internal static string Command(string executable, IReadOnlyList<string> arguments) =>
        string.Join(' ', new[] { executable }.Concat(arguments).Select(Quote));

    private static string Quote(string part) =>
        part.StartsWith("--", StringComparison.Ordinal) && !part.Contains(' ', StringComparison.Ordinal)
            ? part
            : "\"" + (part.EndsWith('\\') ? part + "\\" : part) + "\"";
}
