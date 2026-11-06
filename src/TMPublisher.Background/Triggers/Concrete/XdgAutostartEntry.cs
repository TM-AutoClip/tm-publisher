using Microsoft.Extensions.Logging;

namespace TMPublisher.Background.Triggers;

/// <summary>
/// Linux without a systemd user session: an XDG autostart entry (<c>$XDG_CONFIG_HOME/autostart/&lt;name&gt;.desktop</c>) that runs
/// <c>--run-due</c> when the user signs in to the desktop. There is no start at a post's time; between sign-ins only the open window sends.
/// </summary>
public sealed class XdgAutostartEntry(ILogger<XdgAutostartEntry> logger)
{
    /// <summary>The configuration folder (<c>$XDG_CONFIG_HOME</c>, or <c>~/.config</c>).</summary>
    internal static string ConfigFolder
    {
        get
        {
            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            return string.IsNullOrEmpty(config) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config") : config;
        }
    }

    /// <summary>Writes the entry.</summary>
    public void Write(TriggerPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var file = FileOf(plan.Name);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, Text(plan));
        logger.LogInformation("Autostart entry written (sign-in only: no systemd user session)");
    }

    /// <summary>Removes the entry; <c>true</c> when there was one.</summary>
    public bool Remove(string name)
    {
        var file = FileOf(name);
        if (!File.Exists(file))
        {
            return false;
        }

        File.Delete(file);
        logger.LogInformation("Autostart entry removed");
        return true;
    }

    /// <summary>Whether the entry exists.</summary>
    public static bool Exists(string name) => File.Exists(FileOf(name));

    /// <summary>The entry text.</summary>
    public static string Text(TriggerPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return string.Join('\n',
            "[Desktop Entry]",
            "Type=Application",
            "Name=TM Publisher (send due posts)",
            "Comment=Sends the scheduled posts of TM Publisher that are due, then ends",
            "Exec=" + Exec(plan.Program, plan.Arguments),
            "Terminal=false",
            "NoDisplay=true",
            "X-GNOME-Autostart-enabled=true",
            string.Empty);
    }

    /// <summary>
    /// The Exec value (Desktop Entry specification): each part in double quotes with <c>" ` $ \</c> escaped, then the value escaped as a
    /// string (backslash doubled) and <c>%</c> written as <c>%%</c>.
    /// </summary>
    internal static string Exec(string executable, IReadOnlyList<string> arguments) =>
        string.Join(' ', new[] { executable }.Concat(arguments).Select(Quote)).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "%%", StringComparison.Ordinal);

    private static string FileOf(string name) => Path.Combine(ConfigFolder, "autostart", name + ".desktop");

    private static string Quote(string part) =>
        part.StartsWith("--", StringComparison.Ordinal) && !part.Contains(' ', StringComparison.Ordinal)
            ? part
            : "\"" + part.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("`", "\\`", StringComparison.Ordinal).Replace("$", "\\$", StringComparison.Ordinal) + "\"";
}
