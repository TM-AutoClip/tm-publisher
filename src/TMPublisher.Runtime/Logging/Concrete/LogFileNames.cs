using System.Globalization;
using System.Text.RegularExpressions;

namespace TMPublisher.Runtime.Logging;

/// <summary>
/// Names of the log files: <c>&lt;kind&gt;-yyyy-MM-dd.log</c> (local date), and the names of 0.1.0–0.1.3
/// (<c>tmpublisher[-scheduler|-notify]-yyyyMMdd.log</c>) so retention also removes those. The old files never go into a report zip: 0.1.3
/// still wrote problem texts that name the account. Any other file in the folder is not ours and is left alone.
/// </summary>
public static partial class LogFileNames
{
    /// <summary>The file of a kind on a day.</summary>
    public static string For(string kind, DateOnly day) => kind + "-" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log";

    /// <summary>
    /// The date in the name of one of our log files; <c>false</c> for any other file name, and for a 0.1.0–0.1.3 name when
    /// <paramref name="includeLegacy"/> is <c>false</c>.
    /// </summary>
    public static bool TryGetDate(string fileName, out DateOnly day, bool includeLegacy = true)
    {
        day = default;
        var current = Current().Match(fileName);
        if (current.Success && LogKinds.All.Contains(current.Groups[1].Value, StringComparer.Ordinal))
        {
            return DateOnly.TryParseExact(current.Groups[2].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
        }

        if (!includeLegacy)
        {
            return false;
        }

        var legacy = Legacy().Match(fileName);
        return legacy.Success && DateOnly.TryParseExact(legacy.Groups[1].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
    }

    [GeneratedRegex(@"^([a-z]+)-(\d{4}-\d{2}-\d{2})\.log$")]
    private static partial Regex Current();

    [GeneratedRegex(@"^tmpublisher-(?:scheduler-|notify-)?(\d{8})\.log$")]
    private static partial Regex Legacy();
}
