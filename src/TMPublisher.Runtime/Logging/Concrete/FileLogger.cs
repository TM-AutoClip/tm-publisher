using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Runtime.Logging;

/// <summary>One category of <see cref="FileLoggerProvider"/>: Information and above, one line per entry (the exception follows it).</summary>
internal sealed class FileLogger(string category, DailyLogFile file) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel is >= LogLevel.Information and < LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(formatter);
        var text = new StringBuilder(160)
            .Append(file.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
            .Append(" [").Append(Level(logLevel)).Append("] ").Append(category);
        if (eventId.Id != 0)
        {
            text.Append('#').Append(eventId.Id.ToString(CultureInfo.InvariantCulture));
        }

        text.Append(": ").Append(formatter(state, exception));
        if (exception is not null)
        {
            text.Append(Environment.NewLine).Append(exception);
        }

        file.Write(LogScrubber.Scrub(text.ToString()));
    }

    private static string Level(LogLevel level) => level switch
    {
        LogLevel.Information => "Information",
        LogLevel.Warning => "Warning",
        LogLevel.Error => "Error",
        LogLevel.Critical => "Critical",
        _ => level.ToString(),
    };
}
