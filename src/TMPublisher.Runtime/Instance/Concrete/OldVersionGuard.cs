using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Instance;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Runtime.Instance;

/// <summary>
/// Finds a running TM Publisher 0.1.0–0.1.2 of the same data folder. Those versions hold the named mutex <c>TMPublisher-&lt;hash&gt;</c>, owned by
/// their main thread for the life of the process; a second start of them that finds the name already there only signals the first and ends.
/// <para>
/// Windows: the mutex counts only while somebody owns it. The new background scheduler keeps the name open without owning it
/// (<see cref="HoldName"/>), so an old version started later finds it and ends at once, and that handle is not mistaken for an old version.
/// </para>
/// <para>
/// Linux: .NET keeps a named mutex in a file under <c>.dotnet/shm/session&lt;id&gt;/</c> (below <c>$XDG_RUNTIME_DIR</c>, seen on a Raspberry
/// Pi as <c>/run/user/&lt;uid&gt;</c>, or <c>$TMPDIR</c> or <c>/tmp</c>; all of them are looked at, since a start from a terminal may not
/// have the same variables as one from the desktop), and every process that has it open holds a shared lock on that file. The mutex itself is visible only in the same login session,
/// and not at all to the natively compiled scheduler (its named mutexes live inside the process), so the files of every session are looked at
/// instead: a file that cannot be locked exclusively is open in a running 0.1.x process. Nothing in 0.1.3 opens that name on Linux.
/// </para>
/// </summary>
public sealed class OldVersionGuard : IOldVersionCheck
{
    private const int OwnerTries = 3;
    private static readonly TimeSpan OwnerRetry = TimeSpan.FromMilliseconds(50);
    private readonly IReadOnlyList<string> _roots;
    private readonly ILogger<OldVersionGuard> _logger;
    private int _warned;

    /// <summary>Creates for the data folder of <paramref name="paths"/>.</summary>
    public OldVersionGuard(AppPaths paths, ILogger<OldVersionGuard> logger)
        : this(paths?.DataDirectory ?? throw new ArgumentNullException(nameof(paths)), null, logger)
    {
    }

    /// <summary>Creates with the folders that may hold .NET's shared memory files (tests); <c>null</c>: the ones of this system.</summary>
    internal OldVersionGuard(string dataDirectory, IReadOnlyList<string>? roots, ILogger<OldVersionGuard> logger)
    {
        Name = NameOf(dataDirectory);
        _logger = logger;
        _roots = roots ?? (OperatingSystem.IsWindows() ? [] : RuntimeRoots(logger));
    }

    /// <summary>The single-instance name 0.1.0–0.1.2 used for the data folder.</summary>
    public string Name { get; }

    /// <summary>The single-instance name 0.1.0–0.1.2 used for <paramref name="dataDirectory"/> (the same expression as theirs).</summary>
    public static string NameOf(string dataDirectory) =>
        "TMPublisher-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataDirectory).ToUpperInvariant())))[..16];

    /// <inheritdoc />
    public bool OldVersionRuns() => OperatingSystem.IsWindows() ? MutexOwned() : AnyFileHeld();

    /// <summary>
    /// Windows: keeps the old single-instance name open (not owned) until the result is disposed, so a 0.1.0–0.1.2 started later is a "second
    /// start" and ends without sending anything. <c>null</c> on Linux, where the name would not reach every session and would make the file check
    /// find this process.
    /// </summary>
    public IDisposable? HoldName() => OperatingSystem.IsWindows() ? new Mutex(false, Name, out _) : null;

    /// <summary>Whether another process holds <paramref name="file"/> open with a lock (a shared lock on Linux, a share mode on Windows).</summary>
    internal bool IsHeld(string file)
    {
        try
        {
            // .NET takes flock(LOCK_EX | LOCK_NB) for FileShare.None on Linux: it fails while any process holds its shared lock.
            using var probe = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // The process ended and removed the file meanwhile.
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            WarnOnce(ex, "a .NET shared memory file could not be opened");
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>Whether a 0.1.x mutex file of the data folder is open in any session below the roots.</summary>
    internal bool AnyFileHeld()
    {
        foreach (var root in _roots)
        {
            try
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (var runtime in Directory.EnumerateDirectories(root, ".dotnet*"))
                {
                    var shm = Path.Combine(runtime, "shm");
                    if (!Directory.Exists(shm))
                    {
                        continue;
                    }

                    foreach (var scope in Directory.EnumerateDirectories(shm))
                    {
                        var file = Path.Combine(scope, Name);
                        if (File.Exists(file) && IsHeld(file))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Another user's session folder, or one removed while it was read: the other folders are still looked at.
                WarnOnce(ex, "a .NET shared memory folder could not be read");
            }
        }

        return false;
    }

    private static string[] RuntimeRoots(ILogger logger) =>
        [.. new[] { Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"), OwnRunDirectory(logger), Environment.GetEnvironmentVariable("TMPDIR"), "/tmp" }
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => Path.TrimEndingDirectorySeparator(r!))
            .Distinct(StringComparer.Ordinal)];

    /// <summary><c>/run/user/&lt;uid&gt;</c> of this user (from <c>/proc/self/status</c>), also when <c>$XDG_RUNTIME_DIR</c> is not set here.</summary>
    private static string? OwnRunDirectory(ILogger logger)
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/self/status"))
            {
                if (line.StartsWith("Uid:", StringComparison.Ordinal)
                    && line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) is [_, var uid, ..])
                {
                    return "/run/user/" + uid;
                }
            }

            logger.LogWarning("Looking for an old TM Publisher: no user id in /proc/self/status; /run/user is skipped");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Looking for an old TM Publisher: /proc/self/status could not be read ({Type}); /run/user is skipped", ex.GetType().Name);
        }

        return null;
    }

    private bool MutexOwned()
    {
        Mutex? mutex;
        try
        {
            if (!Mutex.TryOpenExisting(Name, out mutex))
            {
                return false;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            // The name belongs to another user's process: not a TM Publisher of this user.
            WarnOnce(ex, "the old single-instance name could not be opened");
            return false;
        }

        using (mutex)
        {
            // Tried a few times: another 0.1.3 process may own it for a moment while it looks; an old version owns it for its whole life.
            for (var i = 0; i < OwnerTries; i++)
            {
                if (i > 0)
                {
                    Thread.Sleep(OwnerRetry);
                }

                bool acquired;
                try
                {
                    acquired = mutex.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    // Its owner ended without releasing it: no old version runs (the mutex is owned here now and released below).
                    acquired = true;
                }

                if (acquired)
                {
                    mutex.ReleaseMutex();
                    return false;
                }
            }
        }

        return true;
    }

    private void WarnOnce(Exception ex, string what)
    {
        // Logged once per process with the reason; the path is left out (it may contain the user's folder name).
        if (Interlocked.Exchange(ref _warned, 1) == 0)
        {
            _logger.LogWarning("Looking for an old TM Publisher: {What} ({Type}, 0x{Code:X8}); that place is skipped", what, ex.GetType().Name, ex.HResult);
        }
    }
}
