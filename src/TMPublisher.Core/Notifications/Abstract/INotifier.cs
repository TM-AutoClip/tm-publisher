namespace TMPublisher.Core.Notifications;

/// <summary>Shows notifications (the desktop app's bottom-right corner window; respects the user's settings).</summary>
public interface INotifier
{
    /// <summary>Shows the notification (does nothing when notifications are off).</summary>
    void Notify(AppNotification notification);
}
