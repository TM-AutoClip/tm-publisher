using TMPublisher.Desktop.Paths;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// The arguments an autostart entry starts the app with: <c>--tray</c>, and <c>--data-dir &lt;folder&gt;</c> when this run uses another data
/// folder than the default one. Without it a sign-in would open the empty default folder and the real schedule would never run.
/// </summary>
public static class AutostartArguments
{
    /// <summary>The arguments for the data folder of <paramref name="paths"/>.</summary>
    public static IReadOnlyList<string> For(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.IsDefaultDirectory ? ["--tray"] : ["--tray", "--data-dir", paths.DataDirectory];
    }
}
