using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Tests.Runtime;

/// <summary>Keeps every log entry (level, category, text, exception) in memory.</summary>
internal sealed class RecordingLoggerProvider : ILoggerProvider, ILogger
{
    private string _category = string.Empty;

    public ConcurrentQueue<(LogLevel Level, string Category, string Text, Exception? Exception)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new RecordingLoggerProvider { _category = categoryName, Shared = Entries };

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        (Shared ?? Entries).Enqueue((logLevel, _category, formatter(state, exception), exception));

    public void Dispose()
    {
        // Nothing to release.
    }

    private ConcurrentQueue<(LogLevel Level, string Category, string Text, Exception? Exception)>? Shared { get; init; }
}
