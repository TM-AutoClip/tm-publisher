namespace TMPublisher.Desktop.Paths;

/// <summary>Command line options of the program.</summary>
public sealed record StartOptions(bool RunDue, string? DataDirectory, bool Gpu = false, bool Notify = false)
{
    /// <summary>
    /// Parses <c>--data-dir &lt;folder&gt;</c> (the environment variable TMPUBLISHER_DATA_DIR is the fallback), <c>--gpu</c> (draw with the
    /// graphics card instead of the processor; or TMPUBLISHER_GPU=1), <c>--notify</c> (only show the notifications read from standard input)
    /// and <c>--run-due</c> (no window: send what is due, set the system scheduler's next start, end). <c>--tray</c>, which the autostart entry
    /// of 0.1.0–0.1.2 passes, means <c>--run-due</c> (the run removes that entry).
    /// </summary>
    public static StartOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var runDue = args.Contains("--run-due", StringComparer.Ordinal) || args.Contains("--tray", StringComparer.Ordinal);
        var notify = args.Contains("--notify", StringComparer.Ordinal);
        var gpu = args.Contains("--gpu", StringComparer.Ordinal) || Environment.GetEnvironmentVariable("TMPUBLISHER_GPU") == "1";
        string? data = null;
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (args[i] == "--data-dir")
            {
                data = args[i + 1];
            }
        }

        data ??= Environment.GetEnvironmentVariable("TMPUBLISHER_DATA_DIR");
        return new StartOptions(runDue, string.IsNullOrWhiteSpace(data) ? null : Path.GetFullPath(data), gpu, notify);
    }
}
