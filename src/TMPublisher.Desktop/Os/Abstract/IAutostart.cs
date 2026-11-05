namespace TMPublisher.Desktop.Os;

/// <summary>
/// Starting the background scheduler with the operating system session. Written on first setup and when the user changes the setting, and moved
/// to the scheduler once when it still starts an older program (0.1.0–0.1.2 started the window with <c>--tray</c>).
/// </summary>
public interface IAutostart
{
    /// <summary>Whether it is on now.</summary>
    bool IsEnabled { get; }

    /// <summary>Turns it on or off.</summary>
    void Set(bool enabled);

    /// <summary>When it is the window-program entry 0.1.0–0.1.2 wrote for this data folder, points it to the background scheduler (on stays on, off stays off).</summary>
    void Repair();
}
