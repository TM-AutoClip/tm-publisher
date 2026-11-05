namespace TMPublisher.Desktop.Os;

/// <summary>Starting with the operating system session (in the tray). Written only on first setup and when the user changes the setting.</summary>
public interface IAutostart
{
    /// <summary>Whether it is on now.</summary>
    bool IsEnabled { get; }

    /// <summary>Turns it on or off.</summary>
    void Set(bool enabled);
}
