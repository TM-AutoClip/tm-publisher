using TMPublisher.Runtime.Ipc;

namespace TMPublisher.Background.Os;

/// <summary>The scheduler's tray icon (Windows), or nothing (Linux: the app menu entry opens the window).</summary>
public interface ITrayIcon
{
    /// <summary>"Open TM Publisher" was chosen (or the icon clicked).</summary>
    event EventHandler? OpenRequested;

    /// <summary>"Pause scheduling" / "Resume scheduling" was chosen.</summary>
    event EventHandler? PauseToggled;

    /// <summary>"Quit" was chosen.</summary>
    event EventHandler? QuitRequested;

    /// <summary>Shows the state (tooltip, menu text).</summary>
    void Update(SchedulerState state);

    /// <summary>Removes the icon (the process ends).</summary>
    void Remove();
}
