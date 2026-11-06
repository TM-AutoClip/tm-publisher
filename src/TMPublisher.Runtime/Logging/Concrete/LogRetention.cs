namespace TMPublisher.Runtime.Logging;

/// <summary>
/// Deletes our log files dated <see cref="LogLimits.KeepDays"/> or more days before today, by the date in the name (not the write time: a
/// copied or touched file keeps its day). Files with other names are left alone; a file that cannot be deleted is reported and the rest go on.
/// </summary>
public static class LogRetention
{
    /// <summary>Whether a file of <paramref name="day"/> is still kept on <paramref name="today"/>.</summary>
    public static bool Keeps(DateOnly day, DateOnly today) => day > today.AddDays(-LogLimits.KeepDays);

    /// <summary>Deletes the old files; returns the names deleted. <paramref name="failed"/> gets each file that could not be deleted.</summary>
    public static IReadOnlyList<string> Clean(string directory, DateOnly today, Action<string, Exception> failed)
    {
        ArgumentNullException.ThrowIfNull(failed);
        var deleted = new List<string>();
        if (!Directory.Exists(directory))
        {
            return deleted;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.log"))
        {
            var name = Path.GetFileName(file);
            if (!LogFileNames.TryGetDate(name, out var day) || Keeps(day, today))
            {
                continue;
            }

            try
            {
                File.Delete(file);
                deleted.Add(name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed(name, ex);
            }
        }

        return deleted;
    }
}
