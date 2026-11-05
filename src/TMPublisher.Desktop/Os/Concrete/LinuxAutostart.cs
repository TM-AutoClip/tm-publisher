using Microsoft.Extensions.Logging;
using TMPublisher.Runtime.Os;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// freedesktop autostart of the background scheduler: <c>$XDG_CONFIG_HOME/autostart/tm-publisher.desktop</c> (~/.config/autostart). Turning it off writes
/// <c>Hidden=true</c>, which the specification defines as "disabled" and which also hides a system-wide entry of the same name.
/// </summary>
public sealed class LinuxAutostart(AppPaths paths, ILogger<LinuxAutostart> logger) : IAutostart
{
    private static string File
    {
        get
        {
            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var root = string.IsNullOrEmpty(config) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config") : config;
            return Path.Combine(root, "autostart", "tm-publisher.desktop");
        }
    }

    /// <inheritdoc />
    public bool IsEnabled => System.IO.File.Exists(File) && !System.IO.File.ReadAllText(File).Contains("Hidden=true", StringComparison.Ordinal);

    /// <inheritdoc />
    public void Set(bool enabled)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        var exec = Expected();
        System.IO.File.WriteAllText(File, string.Join('\n',
            "[Desktop Entry]",
            "Type=Application",
            "Name=TM Publisher scheduler",
            "Comment=Sends the scheduled posts of TM Publisher in the background",
            "Exec=" + exec,
            "Terminal=false",
            "X-GNOME-Autostart-enabled=" + (enabled ? "true" : "false"),
            "Hidden=" + (enabled ? "false" : "true"),
            string.Empty));
        logger.LogInformation("Autostart {State}{Folder}", enabled ? "enabled" : "disabled", paths.IsDefaultDirectory ? string.Empty : " (own data folder)");
    }

    /// <inheritdoc />
    public void Repair()
    {
        if (!System.IO.File.Exists(File) || ProgramFiles.Scheduler is null)
        {
            return;
        }

        if (System.IO.File.ReadAllLines(File).FirstOrDefault(l => l.StartsWith("Exec=", StringComparison.Ordinal)) is { } exec
            && AutostartArguments.IsOldEntryOf(paths, exec))
        {
            var enabled = IsEnabled;
            Set(enabled);
            logger.LogInformation("Autostart entry moved to the background scheduler ({State})", enabled ? "on" : "off");
        }
    }

    private string Expected() => Exec(AutostartArguments.Program(), AutostartArguments.For(paths));

    /// <summary>
    /// The Exec value (Desktop Entry specification): each part in double quotes with <c>" ` $ \</c> escaped, then the value escaped as a
    /// string (backslash doubled) and <c>%</c> written as <c>%%</c>.
    /// </summary>
    internal static string Exec(string executable, IReadOnlyList<string> arguments) =>
        string.Join(' ', new[] { executable }.Concat(arguments).Select(Quote)).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "%%", StringComparison.Ordinal);

    private static string Quote(string part) =>
        part.StartsWith("--", StringComparison.Ordinal) && !part.Contains(' ', StringComparison.Ordinal)
            ? part
            : "\"" + part.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("`", "\\`", StringComparison.Ordinal).Replace("$", "\\$", StringComparison.Ordinal) + "\"";
}
