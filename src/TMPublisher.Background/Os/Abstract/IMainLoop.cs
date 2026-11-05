namespace TMPublisher.Background.Os;

/// <summary>What keeps the scheduler process alive on its main thread (a Win32 message loop, or a wait for a stop signal on Linux).</summary>
public interface IMainLoop
{
    /// <summary>The operating system asks the process to end (log off, shut down, SIGTERM, Ctrl+C).</summary>
    event EventHandler? StopRequested;

    /// <summary>Runs on the calling thread until <see cref="Quit"/>.</summary>
    void Run();

    /// <summary>Ends <see cref="Run"/> (from any thread).</summary>
    void Quit();
}
