using TMPublisher.Core.Paths;

namespace TMPublisher.Desktop.Paths;

/// <summary>
/// Data folder: the given folder, otherwise %LOCALAPPDATA%\TMPublisher on Windows and $XDG_DATA_HOME/tm-publisher
/// (~/.local/share/tm-publisher) on Linux. Everything the app writes is under it.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    /// <summary>Uses <paramref name="dataDirectory"/> or the default folder.</summary>
    public AppPaths(string? dataDirectory)
    {
        DataDirectory = dataDirectory ?? DefaultDirectory();
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    /// <inheritdoc />
    public string DataDirectory { get; }

    /// <summary>Whether this is the default data folder (not one given with <c>--data-dir</c> or TMPUBLISHER_DATA_DIR).</summary>
    public bool IsDefaultDirectory => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(DataDirectory)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(DefaultDirectory())), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <inheritdoc />
    public string DatabaseFile => Path.Combine(DataDirectory, "publisher.db");

    /// <inheritdoc />
    public string KeyFile => Path.Combine(DataDirectory, "key.json");

    /// <inheritdoc />
    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>Plain preferences readable before the database is unlocked (only the interface language).</summary>
    public string PreferencesFile => Path.Combine(DataDirectory, "preferences.json");

    private static string DefaultDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TMPublisher");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var root = string.IsNullOrEmpty(xdg)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
            : xdg;
        return Path.Combine(root, "tm-publisher");
    }
}
