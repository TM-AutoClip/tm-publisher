namespace TMPublisher.Runtime.Os;

/// <summary>
/// The two programs, side by side in one folder: the window (<c>TMPublisher.exe</c> / <c>tm-publisher</c>) and the background scheduler
/// (<c>TMPublisherScheduler.exe</c> / <c>tm-publisher-scheduler</c>). The build output names (<c>TMPublisher</c>, <c>TMPublisherScheduler</c>)
/// are found too.
/// </summary>
public static class ProgramFiles
{
    /// <summary>The scheduler next to the running program; <c>null</c> when it is not there.</summary>
    public static string? Scheduler => Find(OperatingSystem.IsWindows() ? ["TMPublisherScheduler.exe"] : ["tm-publisher-scheduler", "TMPublisherScheduler"]);

    /// <summary>The window program next to the running program; <c>null</c> when it is not there.</summary>
    public static string? Window => Find(OperatingSystem.IsWindows() ? ["TMPublisher.exe"] : ["tm-publisher", "TMPublisher"]);

    /// <summary>The folder of the running program.</summary>
    public static string Folder => AppContext.BaseDirectory;

    private static string? Find(IEnumerable<string> names) =>
        names.Select(n => Path.Combine(Folder, n)).FirstOrDefault(File.Exists);
}
