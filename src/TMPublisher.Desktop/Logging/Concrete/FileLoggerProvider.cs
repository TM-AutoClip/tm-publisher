using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Desktop.Logging;

/// <summary>
/// Daily log files in the data folder (<c>logs/tmpublisher-yyyyMMdd.log</c>, 14 days kept). Every line goes through the redactor: secrets
/// never reach the file even when a platform echoes them in an error body.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int KeepDays = 14;
    private readonly string _directory;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Writes into <paramref name="directory"/>.</summary>
    public FileLoggerProvider(string directory)
    {
        _directory = directory;
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
        var file = Path.Combine(_directory, "tmpublisher-" + DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + ".log");
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
