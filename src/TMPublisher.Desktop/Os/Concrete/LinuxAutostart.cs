using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Paths;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// freedesktop autostart: <c>$XDG_CONFIG_HOME/autostart/tm-publisher.desktop</c> (~/.config/autostart). Turning it off writes
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
        var exec = Exec(Environment.ProcessPath ?? string.Empty, AutostartArguments.For(paths));
        System.IO.File.WriteAllText(File, string.Join('\n',
            "[Desktop Entry]",
            "Type=Application",
            "Name=TM Publisher",
            "Comment=Publish one post to many social accounts",
            "Exec=" + exec,
            "Terminal=false",
            "X-GNOME-Autostart-enabled=" + (enabled ? "true" : "false"),
            "Hidden=" + (enabled ? "false" : "true"),
            string.Empty));
        logger.LogInformation("Autostart {State}{Folder}", enabled ? "enabled" : "disabled", paths.IsDefaultDirectory ? string.Empty : " (own data folder)");
    }

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
