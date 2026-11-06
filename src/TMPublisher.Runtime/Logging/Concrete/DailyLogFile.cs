using System.Globalization;
using System.Text;

namespace TMPublisher.Runtime.Logging;

/// <summary>
/// The daily file of one process kind (<see cref="LogFileNames.For"/>, local date). The file stays open between lines and is shared for
/// reading and writing (another process of the same kind, an editor, the report zip); every line goes to the current end and is flushed at
/// once. At the first line of a new day the file changes, the header is written into the new one and old files are deleted
/// (<see cref="LogRetention"/>), so nothing extra runs in the background. When a file reaches <see cref="LogLimits.DailyBytes"/> one warning
/// line is written and nothing more until the next day. A file that cannot be opened is tried again at most once a minute (the lines in
/// between are counted and the count is written once it opens). Two processes of one kind can meet briefly (a second start of the window, the
/// notification process being replaced): each line is written under a short lock on one byte far past the end, so neither overwrites the
/// other's line. Thread-safe.
/// </summary>
public sealed class DailyLogFile : IDisposable
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly string _directory;
    private readonly string _kind;
    private readonly string _program;
    private readonly TimeProvider _clock;
    private readonly long _limit;
    // The byte locked around each write (never written: readers and writers of the text are not blocked by it).
    private const long LockOffset = 1L << 62;
    private static readonly TimeSpan RetryOpen = TimeSpan.FromMinutes(1);
    private readonly Lock _gate = new();
    private FileStream? _stream;
    private DateOnly _day;
    private bool _full;
    private bool _disposed;
    private bool _lockUnsupported;
    private DateTimeOffset _retryAt;
    private long _dropped;

    /// <summary>Writes <paramref name="kind"/>'s files into <paramref name="directory"/>; <paramref name="program"/> names it in the header.</summary>
    public DailyLogFile(string directory, string kind, string program, TimeProvider? clock = null, long limit = LogLimits.DailyBytes)
    {
        _directory = directory;
        _kind = kind;
        _program = program;
        _clock = clock ?? TimeProvider.System;
        _limit = limit;
        Directory.CreateDirectory(directory);
        lock (_gate)
        {
            Open(Today(), clean: true);
        }
    }

    /// <summary>The file written now.</summary>
    public string CurrentFile => Path.Combine(_directory, LogFileNames.For(_kind, _day));

    /// <summary>The local time now (the time stamp of a line).</summary>
    public DateTimeOffset Now => _clock.GetLocalNow();

    /// <summary>Appends one line (the day change and the size limit are handled here).</summary>
    public void Write(string line)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var today = Today();
            if (today != _day)
            {
                Open(today, clean: true);
            }
            else if (_stream is null && !_full && _clock.GetUtcNow() >= _retryAt)
            {
                Open(today, clean: false);
            }

            if (_full)
            {
                return;
            }

            if (_stream is null)
            {
                _dropped++;
                return;
            }

            var bytes = Utf8.GetBytes(line + Environment.NewLine);
            if (Length() + bytes.Length > _limit)
            {
                _full = true;
                Append(Stamp() + " [Warning] TMPublisher.Logging: The log file reached its daily limit of "
                    + (_limit / (1024 * 1024)).ToString(CultureInfo.InvariantCulture) + " MB; nothing more is written to it today");
                return;
            }

            Append(bytes);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _stream?.Dispose();
            _stream = null;
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    private string Stamp() => _clock.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);

    private long Length()
    {
        try
        {
            return _stream!.Length;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Opens the file of <paramref name="day"/> and writes the header; with <paramref name="clean"/> (at start and at each day change, not on a
    /// retry within the day) old files are deleted too.
    /// </summary>
    private void Open(DateOnly day, bool clean)
    {
        _stream?.Dispose();
        _stream = null;
        if (day != _day)
        {
            _dropped = 0;
        }

        _day = day;
        _full = false;
        var problems = new List<string>();
        try
        {
            _stream = new FileStream(CurrentFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1);
            _full = _stream.Length >= _limit;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The log itself cannot be written: the error output is the last place left (nothing else would show it). Tried again later.
            _retryAt = _clock.GetUtcNow() + RetryOpen;
            Console.Error.WriteLine("TM Publisher: the log file could not be opened (" + ex.GetType().Name + ": " + LogScrubber.Scrub(ex.Message) + ")");
        }

        if (clean)
        {
            LogRetention.Clean(_directory, day, (name, ex) => problems.Add(name + " (" + ex.GetType().Name + ": " + ex.Message + ")"));
        }

        if (_stream is null || _full)
        {
            return;
        }

        Append(Stamp() + " [Information] TMPublisher.Logging: " + LogHeader.Line(_program));
        if (_dropped > 0)
        {
            Append(Stamp() + " [Warning] TMPublisher.Logging: The log file could not be opened for a while; "
                + _dropped.ToString(CultureInfo.InvariantCulture) + " line(s) were not written");
            _dropped = 0;
        }

        foreach (var problem in problems)
        {
            Append(LogScrubber.Scrub(Stamp() + " [Warning] TMPublisher.Logging: An old log file could not be deleted: " + problem));
        }
    }

    private void Append(string line) => Append(Utf8.GetBytes(line + Environment.NewLine));

    private void Append(byte[] bytes)
    {
        var locked = false;
        try
        {
            // Another process of the same kind (or an editor) may have written meanwhile: always at the current end, under the lock.
            locked = TryLock();
            _stream!.Seek(0, SeekOrigin.End);
            _stream.Write(bytes);
            _stream.Flush();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            Console.Error.WriteLine("TM Publisher: a log line could not be written (" + ex.GetType().Name + ": " + LogScrubber.Scrub(ex.Message) + ")");
        }
        finally
        {
            if (locked)
            {
                Unlock();
            }
        }
    }

    /// <summary>
    /// Takes the write lock, waiting up to about half a second for another process; <c>false</c> when it could not (the line is then written
    /// without it: losing it would be worse than a rare overlap).
    /// </summary>
    private bool TryLock()
    {
        if (_lockUnsupported || OperatingSystem.IsMacOS())
        {
            // macOS has no byte-range locks in .NET (not a target of this program; lines are written as before).
            return false;
        }

        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                _stream!.Lock(LockOffset, 1);
                return true;
            }
            catch (PlatformNotSupportedException)
            {
                // A system without byte-range locks: lines are written as before (at the end, without the lock).
                _lockUnsupported = true;
                Console.Error.WriteLine("TM Publisher: this system has no file locks; log lines are written without one");
                return false;
            }
            catch (IOException)
            {
                // Held by the other process for its one line.
                Thread.Sleep(5);
            }
        }

        Console.Error.WriteLine("TM Publisher: the log file lock stayed busy; the line is written without it");
        return false;
    }

    private void Unlock()
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            _stream?.Unlock(LockOffset, 1);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The handle is closed or the lock is gone already: nothing is held any more.
            Console.Error.WriteLine("TM Publisher: the log file lock could not be released (" + ex.GetType().Name + ")");
        }
    }
}
