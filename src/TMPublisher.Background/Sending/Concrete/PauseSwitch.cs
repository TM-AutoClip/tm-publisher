using TMPublisher.Runtime.Paths;

namespace TMPublisher.Background.Sending;

/// <summary>
/// "Pause scheduling", kept across starts as a small file in the data folder (<c>scheduler-paused.txt</c>, the name 0.1.3–0.1.4 used): the
/// window and the runs the system scheduler starts both look at it, so a pause holds while the window is closed too.
/// </summary>
public sealed class PauseSwitch(AppPaths paths)
{
    private string File => Path.Combine(paths.DataDirectory, "scheduler-paused.txt");

    /// <summary>Whether sending is paused.</summary>
    public bool IsPaused => System.IO.File.Exists(File);

    /// <summary>Pauses or resumes (an I/O error reaches the caller).</summary>
    public void Set(bool paused)
    {
        if (paused)
        {
            System.IO.File.WriteAllText(File, "Scheduling was paused in TM Publisher. Choose Resume there (or delete this file) to send posts again.\n");
        }
        else
        {
            System.IO.File.Delete(File);
        }
    }
}
