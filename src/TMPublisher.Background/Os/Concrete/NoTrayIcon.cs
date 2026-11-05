using TMPublisher.Runtime.Ipc;

namespace TMPublisher.Background.Os;

/// <summary>Linux: no tray icon (a tray host is not there on every desktop); the app menu entry opens the window, which can stop the scheduler.</summary>
public sealed class NoTrayIcon : ITrayIcon
{
    /// <inheritdoc />
    public event EventHandler? OpenRequested
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public event EventHandler? PauseToggled
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public event EventHandler? QuitRequested
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public void Update(SchedulerState state)
    {
        // Nothing to show.
    }

    /// <inheritdoc />
    public void Remove()
    {
        // Nothing to remove.
    }
}
