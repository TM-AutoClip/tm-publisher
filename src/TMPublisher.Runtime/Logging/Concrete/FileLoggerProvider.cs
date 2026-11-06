using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Runtime.Logging;

/// <summary>
/// Daily log files in the data folder, one file per process kind and day (<c>logs/app-yyyy-MM-dd.log</c> for the window,
/// <c>logs/run-yyyy-MM-dd.log</c> for the runs the system scheduler starts, <c>logs/notify-yyyy-MM-dd.log</c> for the notification process),
/// Information and above, the last <see cref="LogLimits.KeepDays"/> days kept (<see cref="DailyLogFile"/>). Every line goes through
/// <see cref="LogScrubber"/>: secrets and personal data never reach the file even when a platform echoes them in an error body.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);

    /// <summary>Writes <paramref name="kind"/>'s file (<see cref="LogKinds"/>) into <paramref name="directory"/>.</summary>
    public FileLoggerProvider(string directory, string kind, TimeProvider? clock = null, long limit = LogLimits.DailyBytes)
    {
        File = new DailyLogFile(directory, kind, Program(kind), clock, limit);
    }

    /// <summary>The file.</summary>
    public DailyLogFile File { get; }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, File));

    /// <inheritdoc />
    public void Dispose()
    {
        _loggers.Clear();
        File.Dispose();
    }

    private static string Program(string kind) => kind switch
    {
        LogKinds.App => "TM Publisher",
        LogKinds.Run => "TM Publisher run",
        LogKinds.Scheduler => "TM Publisher scheduler",
        LogKinds.Notify => "TM Publisher notifications",
        _ => "TM Publisher " + kind,
    };
}
