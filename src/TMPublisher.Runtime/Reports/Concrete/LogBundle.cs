using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using TMPublisher.Runtime.Logging;

namespace TMPublisher.Runtime.Reports;

/// <summary>
/// The zip a user can attach to a problem report, made only when they ask for it and never sent by the program:
/// <c>&lt;program&gt;-logs-yyyyMMdd-HHmm.zip</c> in the reports folder with <c>system.txt</c> and the log files of the last
/// <see cref="LogLimits.KeepDays"/> days (0.1.4 names only, masked once more by <see cref="LogScrubber"/>, line by line so a full 20 MB file
/// is never held in memory). The zip is written under a <c>.part</c> name and renamed when complete, so a failure leaves no half zip to attach.
/// Only the newest <see cref="LogLimits.KeptReports"/> zips stay; the one just made always stays.
/// </summary>
public static partial class LogBundle
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes the zip; returns its path. <paramref name="removed"/> gets each file that could not be removed (an older zip, or the half-written
    /// one after a failure).
    /// </summary>
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

        var part = target + ".part";
        var today = DateOnly.FromDateTime(now.DateTime);
        try
        {
            using (var archive = ZipFile.Open(part, ZipArchiveMode.Create))
            {
                using (var writer = Entry(archive, "system.txt"))
                {
                    writer.Write(LogScrubber.Scrub(systemText, home));
                }

                foreach (var file in LogFiles(logDirectory, today))
                {
                    AddLog(archive, file, home);
                }
            }

            File.Move(part, target);
        }
        catch
        {
            // Our own half-written file: removed so it cannot be attached by mistake; the caller logs and shows the failure.
            TryDelete(part, removed);
            throw;
        }

        Prune(reportDirectory, program, target, removed);
        return target;
    }

    /// <summary>Our log files (0.1.4 names) of the last <see cref="LogLimits.KeepDays"/> days, oldest first.</summary>
    public static IReadOnlyList<string> LogFiles(string logDirectory, DateOnly today)
    {
        if (!Directory.Exists(logDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(logDirectory, "*.log")
            .Select(f => (File: f, Ok: LogFileNames.TryGetDate(Path.GetFileName(f), out var day, includeLegacy: false), Day: day))
            .Where(f => f.Ok && f.Day <= today && LogRetention.Keeps(f.Day, today))
            .OrderBy(f => f.Day).ThenBy(f => Path.GetFileName(f.File), StringComparer.Ordinal)
            .Select(f => f.File)
            .ToList();
    }

    private static void AddLog(ZipArchive archive, string file, string home)
    {
        // The current file is open in this (and maybe another) process: read it shared.
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var writer = Entry(archive, "logs/" + Path.GetFileName(file));
        while (reader.ReadLine() is { } line)
        {
            writer.Write(LogScrubber.Scrub(line, home));
            writer.Write('\n');
        }
    }

    private static StreamWriter Entry(ZipArchive archive, string name) =>
        new(archive.CreateEntry(name, CompressionLevel.Optimal).Open(), Utf8);

    private static void Prune(string reportDirectory, string program, string made, Action<string, Exception>? removed)
    {
        var old = Directory.EnumerateFiles(reportDirectory, program + "-logs-*.zip")
            .Select(f => (File: f, Match: ReportName().Match(Path.GetFileName(f))))
            .Where(f => f.Match.Success)
            // By the minute in the name, then by the number after it (none = 1): "-1430-10" is newer than "-1430-9" and "-1430".
            .OrderByDescending(f => f.Match.Groups[1].Value, StringComparer.Ordinal)
            .ThenByDescending(f => f.Match.Groups[2].Success ? int.Parse(f.Match.Groups[2].Value, CultureInfo.InvariantCulture) : 1)
            .Select(f => f.File)
            .Where(f => !string.Equals(f, made, StringComparison.OrdinalIgnoreCase))
            .Skip(LogLimits.KeptReports - 1);
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

    private static void TryDelete(string file, Action<string, Exception>? removed)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Reported to the caller; the zip failure itself is thrown on with its own cause.
            removed?.Invoke(Path.GetFileName(file), ex);
        }
    }

    [GeneratedRegex(@"-logs-(\d{8}-\d{4})(?:-(\d{1,9}))?\.zip$")]
    private static partial Regex ReportName();
}
