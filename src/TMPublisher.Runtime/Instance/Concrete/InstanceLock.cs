namespace TMPublisher.Runtime.Instance;

/// <summary>
/// A lock file in the data folder held open without sharing for the life of the process (Windows refuses a second open; .NET takes an
/// exclusive flock on Linux). The system releases it when the process ends, also when it is killed. A file is used, not a named mutex: on
/// Linux .NET scopes a named mutex to the login session, so a start from a terminal and one from the menu (or autostart) would both count as
/// the first. <c>scheduler-instance.lock</c> makes sure only one background scheduler sends the posts of a data folder;
/// <c>window-instance.lock</c> keeps one window.
/// </summary>
public sealed class InstanceLock : IDisposable
{
    /// <summary>The background scheduler's lock file.</summary>
    public const string Scheduler = "scheduler-instance.lock";

    /// <summary>The window's lock file.</summary>
    public const string Window = "window-instance.lock";

    private readonly FileStream _file;

    private InstanceLock(FileStream file) => _file = file;

    /// <summary>
    /// Takes the lock; <c>null</c> when another process holds it (<paramref name="reason"/> says why it was not taken). The folder must exist.
    /// </summary>
    public static InstanceLock? TryAcquire(string dataDirectory, string name, out string reason)
    {
        try
        {
            var stream = new FileStream(Path.Combine(Path.GetFullPath(dataDirectory), name), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            reason = string.Empty;
            return new InstanceLock(stream);
        }
        catch (IOException ex) when (ex is not DirectoryNotFoundException and not PathTooLongException and not FileNotFoundException)
        {
            // Held by the running instance (sharing violation on Windows, flock on Linux).
            reason = ex.GetType().Name + ": " + ex.Message;
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _file.Dispose();
}
