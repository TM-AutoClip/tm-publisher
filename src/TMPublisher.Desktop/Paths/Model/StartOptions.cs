namespace TMPublisher.Desktop.Paths;

/// <summary>Command line options.</summary>
public sealed record StartOptions(bool Tray, string? DataDirectory)
{
    /// <summary>Parses <c>--tray</c> and <c>--data-dir &lt;folder&gt;</c>; the environment variable TMPUBLISHER_DATA_DIR is the fallback.</summary>
    public static StartOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var tray = args.Contains("--tray", StringComparer.Ordinal);
        string? data = null;
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (args[i] == "--data-dir")
            {
                data = args[i + 1];
            }
        }

        data ??= Environment.GetEnvironmentVariable("TMPUBLISHER_DATA_DIR");
        return new StartOptions(tray, string.IsNullOrWhiteSpace(data) ? null : Path.GetFullPath(data));
    }
}
