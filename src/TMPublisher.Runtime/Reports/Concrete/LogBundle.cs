using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using TMPublisher.Runtime.Logging;

namespace TMPublisher.Runtime.Reports;

/// <summary>
/// The zip a user can attach to a problem report, made only when they ask for it and never sent by the program:
/// <c>&lt;program&gt;-logs-yyyyMMdd-HHmm.zip</c> in the reports folder with <c>system.txt</c> and the log files of the last
/// <see cref="LogLimits.KeepDays"/> days (our names only, masked once more by <see cref="LogScrubber"/>). Only the newest
/// <see cref="LogLimits.KeptReports"/> zips stay.
/// </summary>
public static partial class LogBundle
{
    /// <summary>Writes the zip; returns its path. <paramref name="removed"/> gets each older zip that could not be removed.</summary>
    public static string Create(string logDirectory, string reportDirectory, string program, string systemText, DateTimeOffset now,
        Action<string, Exception>? removed = null, string? home = null)
    {
        ArgumentNullException.ThrowIfNull(systemText);
        home ??= LogScrubber.Home;
        Directory.CreateDirectory(reportDirectory);
        var stem = program + "-logs-" + now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var target = Path.Combine(reportDirectory, stem + ".zip");
        for (var i = 2; File.Exists(target); i++)
        {
            target = Path.Combine(reportDirectory, stem + "-" + i.ToString(CultureInfo.InvariantCulture) + ".zip");
        }

        var today = DateOnly.FromDateTime(now.DateTime);
        using (var archive = ZipFile.Open(target, ZipArchiveMode.Create))
        {
            Add(archive, "system.txt", LogScrubber.Scrub(systemText, home));
            foreach (var file in LogFiles(logDirectory, today))
            {
                Add(archive, "logs/" + Path.GetFileName(file), LogScrubber.Scrub(Read(file), home));
            }
        }

        Prune(reportDirectory, program, removed);
        return target;
    }

    /// <summary>Our log files of the last <see cref="LogLimits.KeepDays"/> days, oldest first.</summary>
    public static IReadOnlyList<string> LogFiles(string logDirectory, DateOnly today)
    {
        if (!Directory.Exists(logDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(logDirectory, "*.log")
            .Select(f => (File: f, Ok: LogFileNames.TryGetDate(Path.GetFileName(f), out var day), Day: day))
            .Where(f => f.Ok && f.Day <= today && LogRetention.Keeps(f.Day, today))
            .OrderBy(f => f.Day).ThenBy(f => Path.GetFileName(f.File), StringComparer.Ordinal)
            .Select(f => f.File)
            .ToList();
    }

    private static string Read(string file)
    {
        // The current file is open in this (and maybe another) process: read it shared.
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void Add(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
    }

    private static void Prune(string reportDirectory, string program, Action<string, Exception>? removed)
    {
        var pattern = ReportName();
        var old = Directory.EnumerateFiles(reportDirectory, program + "-logs-*.zip")
            .Where(f => pattern.IsMatch(Path.GetFileName(f)))
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal)
            .Skip(LogLimits.KeptReports);
        foreach (var file in old)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                removed?.Invoke(Path.GetFileName(file), ex);
            }
        }
    }

    [GeneratedRegex(@"-logs-\d{8}-\d{4}(?:-\d+)?\.zip$")]
    private static partial Regex ReportName();
}
