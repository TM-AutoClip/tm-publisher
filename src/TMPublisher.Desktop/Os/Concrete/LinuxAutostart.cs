using Microsoft.Extensions.Logging;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// freedesktop autostart: <c>$XDG_CONFIG_HOME/autostart/tm-publisher.desktop</c> (~/.config/autostart). Turning it off writes
/// <c>Hidden=true</c>, which the specification defines as "disabled" and which also hides a system-wide entry of the same name.
/// </summary>
public sealed class LinuxAutostart(ILogger<LinuxAutostart> logger) : IAutostart
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
        var exec = "\"" + Environment.ProcessPath + "\" --tray";
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
        logger.LogInformation("Autostart {State}", enabled ? "enabled" : "disabled");
    }
}
