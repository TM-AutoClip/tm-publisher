using System.Security.Cryptography;
using System.Text;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Background.Triggers;

/// <summary>
/// The name of the system scheduler's entry of a data folder: <c>TM Publisher 1A2B3C4D</c> (Windows task) / <c>tm-publisher-1a2b3c4d</c>
/// (Linux units), the hash of the data folder so two data folders never share one. <c>TMPUBLISHER_TRIGGER_PREFIX</c> replaces the
/// <c>TM Publisher</c> / <c>tm-publisher</c> part (tests and measurements create their entries under their own name).
/// </summary>
public static class TriggerNames
{
    /// <summary>The environment variable of the name prefix.</summary>
    public const string PrefixVariable = "TMPUBLISHER_TRIGGER_PREFIX";

    /// <summary>The entry name of <paramref name="paths"/>' data folder.</summary>
    public static string For(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(paths.DataDirectory).ToUpperInvariant())))[..8];
        var prefix = Environment.GetEnvironmentVariable(PrefixVariable);
        if (OperatingSystem.IsWindows())
        {
            return (string.IsNullOrWhiteSpace(prefix) ? "TM Publisher" : prefix.Trim()) + " " + hash;
        }

        return (string.IsNullOrWhiteSpace(prefix) ? "tm-publisher" : prefix.Trim()) + "-" + hash.ToLowerInvariant();
    }

    /// <summary>The arguments of a run of <paramref name="paths"/>' data folder.</summary>
    public static IReadOnlyList<string> RunArguments(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.IsDefaultDirectory ? ["--run-due"] : ["--run-due", "--data-dir", paths.DataDirectory];
    }
}
