namespace TMPublisher.Runtime.Os;

/// <summary>
/// The one program file (<c>TMPublisher.exe</c> / <c>tm-publisher</c>): the window, the run the system scheduler starts (<c>--run-due</c>) and
/// the notifications (<c>--notify</c>) are the same file. The scheduler file of 0.1.3–0.1.4 is only looked for to tell the user it can go.
/// </summary>
public static class ProgramFiles
{
    /// <summary>
    /// The running program; <c>null</c> when it cannot be named (started as <c>dotnet TMPublisher.dll</c> without the program file next to it,
    /// a test host).
    /// </summary>
    public static string? Self
    {
        get
        {
            var process = Environment.ProcessPath;
            var name = process is null ? string.Empty : Path.GetFileNameWithoutExtension(process);
            if (process is not null && (name.Equals("TMPublisher", StringComparison.OrdinalIgnoreCase) || name.Equals("tm-publisher", StringComparison.Ordinal)))
            {
                return process;
            }

            return Find(OperatingSystem.IsWindows() ? ["TMPublisher.exe"] : ["tm-publisher", "TMPublisher"]);
        }
    }

    /// <summary>The 0.1.3–0.1.4 scheduler file next to the program; <c>null</c> when it is not there.</summary>
    public static string? OldScheduler => Find(OperatingSystem.IsWindows() ? ["TMPublisherScheduler.exe"] : ["tm-publisher-scheduler", "TMPublisherScheduler"]);

    /// <summary>The folder of the running program.</summary>
    public static string Folder => AppContext.BaseDirectory;

    private static string? Find(IEnumerable<string> names) =>
        names.Select(n => Path.Combine(Folder, n)).FirstOrDefault(File.Exists);
}
