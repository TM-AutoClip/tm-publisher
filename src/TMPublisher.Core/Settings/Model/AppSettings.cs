namespace TMPublisher.Core.Settings;

/// <summary>User settings (one row in the encrypted database).</summary>
public sealed class AppSettings
{
    /// <summary>Interface language: <c>en</c> or <c>tr</c>.</summary>
    public string Language { get; set; } = "en";

    /// <summary>Show bottom-right notifications.</summary>
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>How long a notification stays on screen (seconds; default one minute).</summary>
    public int NotificationSeconds { get; set; } = 60;

    /// <summary>
    /// Send the scheduled posts while the app is closed: the system scheduler (Task Scheduler, systemd user timer) starts it for a moment at a
    /// post's time. Stored under its 0.1.0–0.1.4 name ("start with the system"), which meant the same for the user.
    /// </summary>
    public bool StartWithSystem { get; set; } = true;

    /// <summary>Wake the computer from sleep to send a post (Windows Task Scheduler; off by default).</summary>
    public bool WakeToRun { get; set; }

    /// <summary>TikTok posts go to the inbox as drafts by default (direct post is a per-post choice).</summary>
    public bool TikTokDraftByDefault { get; set; } = true;
}
