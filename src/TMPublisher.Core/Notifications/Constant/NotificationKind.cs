namespace TMPublisher.Core.Notifications;

/// <summary>Kind of a corner notification.</summary>
public enum NotificationKind
{
    /// <summary>Published.</summary>
    Published = 0,

    /// <summary>Needs approval in the platform's app.</summary>
    NeedsApproval = 1,

    /// <summary>Failed.</summary>
    Failed = 2,

    /// <summary>Information (missed post, reconnect needed...).</summary>
    Info = 3,
}
