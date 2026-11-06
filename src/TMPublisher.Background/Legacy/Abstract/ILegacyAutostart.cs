namespace TMPublisher.Background.Legacy;

/// <summary>
/// The autostart entry 0.1.0–0.1.4 wrote (the Run key value on Windows, the XDG autostart file on Linux), removed once this version runs:
/// it would start the always-running scheduler (or the old window with <c>--tray</c>) at every sign-in. Only the entry of this data folder
/// that one of those versions wrote is removed; an entry of another data folder or one the user made is left alone.
/// </summary>
public interface ILegacyAutostart
{
    /// <summary>Removes the entry when it is a leftover of this data folder; <c>true</c> when one was removed.</summary>
    bool RemoveLeftover();
}
