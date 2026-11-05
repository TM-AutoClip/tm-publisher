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

    /// <summary>Start with the operating system session (in the tray).</summary>
    public bool StartWithSystem { get; set; } = true;

    /// <summary>TikTok posts go to the inbox as drafts by default (direct post is a per-post choice).</summary>
    public bool TikTokDraftByDefault { get; set; } = true;
}
