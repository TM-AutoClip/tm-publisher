namespace TMPublisher.Background.Triggers;

/// <summary>The state of the system scheduler's entry of a data folder.</summary>
public enum TriggerState
{
    /// <summary>Off: posts are only sent while the window is open.</summary>
    Off,

    /// <summary>On: the system starts the program at the next post's time, at sign-in and for the safety starts.</summary>
    On,

    /// <summary>Only at sign-in (Linux without a systemd user session: an autostart entry); between sign-ins only the open window sends.</summary>
    SignInOnly,

    /// <summary>It could not be written; the reason is in the message and the log.</summary>
    Failed,
}
