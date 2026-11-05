namespace TMPublisher.Desktop.Paths;

/// <summary>Command line options of the window program.</summary>
public sealed record StartOptions(bool Tray, string? DataDirectory, bool Gpu = false, bool Notify = false)
{
    /// <summary>
    /// Parses <c>--data-dir &lt;folder&gt;</c> (the environment variable TMPUBLISHER_DATA_DIR is the fallback), <c>--gpu</c> (draw with the
    /// graphics card instead of the processor; or TMPUBLISHER_GPU=1), <c>--notify</c> (only show the background scheduler's notifications read
    /// from standard input) and <c>--tray</c> (what the autostart entry of 0.1.0–0.1.2 passes: make sure the background scheduler runs, move
    /// the autostart entry to it, open no window).
    /// </summary>
    public static StartOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var tray = args.Contains("--tray", StringComparer.Ordinal);
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
        return new StartOptions(tray, string.IsNullOrWhiteSpace(data) ? null : Path.GetFullPath(data), gpu, notify);
    }
}
