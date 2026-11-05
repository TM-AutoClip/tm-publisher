using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Runtime.Logging;

/// <summary>
/// Daily log files in the data folder, one per process (<c>logs/tmpublisher-yyyyMMdd.log</c> for the window,
/// <c>logs/tmpublisher-scheduler-yyyyMMdd.log</c> for the background scheduler: two processes appending to one file would overwrite each
/// other's lines), 14 days kept. Every line goes through the redactor: secrets never reach the file even when a platform echoes them in an error
/// body.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int KeepDays = 14;
    private readonly string _directory;
    private readonly string _prefix;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Writes into <paramref name="directory"/>; <paramref name="process"/> names the file (<c>tmpublisher-&lt;process&gt;-…</c>, empty for the window).</summary>
    public FileLoggerProvider(string directory, string process = "")
    {
        _directory = directory;
        _prefix = process.Length == 0 ? "tmpublisher-" : "tmpublisher-" + process + "-";
        Directory.CreateDirectory(directory);
        Cleanup();
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    /// <inheritdoc />
    public void Dispose() => _loggers.Clear();

    /// <summary>Appends one line (thread-safe).</summary>
    internal void Write(string line)
    {
        var file = Path.Combine(_directory, _prefix + DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + ".log");
        lock (_gate)
        {
            try
            {
                File.AppendAllText(file, line + Environment.NewLine);
            }
            catch (IOException ex)
            {
                // The log itself cannot be written: the debugger output is the last place left.
                System.Diagnostics.Debug.WriteLine("Log file write failed: " + ex.Message);
            }
        }
    }

    private void Cleanup()
    {
        foreach (var file in Directory.EnumerateFiles(_directory, "tmpublisher-*.log"))
        {
            if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-KeepDays))
            {
                File.Delete(file);
            }
        }
    }
}
