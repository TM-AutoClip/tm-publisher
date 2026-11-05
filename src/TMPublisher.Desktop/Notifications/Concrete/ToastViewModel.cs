using TMPublisher.Core.Notifications;

namespace TMPublisher.Desktop.Notifications;

/// <summary>One corner notification.</summary>
public sealed class ToastViewModel(AppNotification notification, TimeSpan duration)
{
    /// <summary>Kind.</summary>
    public NotificationKind Kind => notification.Kind;

    /// <summary>Title.</summary>
    public string Title => notification.Title;

    /// <summary>Message.</summary>
    public string Message => notification.Message;

    /// <summary>How long it stays.</summary>
    public TimeSpan Duration => duration;

    /// <summary>Glyph by kind.</summary>
    public string Glyph => Kind switch
    {
        NotificationKind.Published => "✔",
        NotificationKind.NeedsApproval => "✋",
        NotificationKind.Failed => "✖",
        _ => "ℹ",
    };

    /// <summary>Whether it is an error (red stripe).</summary>
    public bool IsError => Kind == NotificationKind.Failed;

    /// <summary>Whether it asks for an action (yellow stripe).</summary>
    public bool IsAttention => Kind == NotificationKind.NeedsApproval;
}
