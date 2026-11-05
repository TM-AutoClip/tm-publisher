namespace TMPublisher.Desktop.Paths;

/// <summary>Command line options.</summary>
public sealed record StartOptions(bool Tray, string? DataDirectory, bool Gpu = false)
{
    /// <summary>
    /// Parses <c>--tray</c>, <c>--data-dir &lt;folder&gt;</c> (the environment variable TMPUBLISHER_DATA_DIR is the fallback) and <c>--gpu</c>
    /// (draw with the graphics card instead of the processor; or TMPUBLISHER_GPU=1, which also reaches the autostart entry).
    /// </summary>
    public static StartOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var tray = args.Contains("--tray", StringComparer.Ordinal);
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
        return new StartOptions(tray, string.IsNullOrWhiteSpace(data) ? null : Path.GetFullPath(data), gpu);
    }
}
