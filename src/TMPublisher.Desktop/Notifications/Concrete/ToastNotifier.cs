using Microsoft.Extensions.Logging;
using TMPublisher.Core.Notifications;
using TMPublisher.Desktop.Startup;
using TMPublisher.Desktop.Views;

namespace TMPublisher.Desktop.Notifications;

/// <summary>
/// The window's notifications (<see cref="ToastStack"/>), following the user's settings: off means nothing is shown, the duration is the
/// user's (default one minute). The background scheduler's notifications arrive already filtered and are shown with their own duration.
/// </summary>
public sealed class ToastNotifier(AppSession session, ToastStack stack, ILogger<ToastNotifier> logger) : INotifier
{
    /// <summary>Open windows (tests).</summary>
    internal IReadOnlyList<ToastWindow> Open => stack.Open;

    /// <inheritdoc />
    public void Notify(AppNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        logger.LogInformation("Notification ({Kind}): {Title}", notification.Kind, notification.Title);
        if (session.Settings.NotificationsEnabled)
        {
            stack.Show(notification, TimeSpan.FromSeconds(Math.Max(3, session.Settings.NotificationSeconds)));
        }
    }

    /// <summary>Shows a notification of the background scheduler (it applied the settings already).</summary>
    public void ShowFromScheduler(AppNotification notification, int seconds)
    {
        ArgumentNullException.ThrowIfNull(notification);
        logger.LogInformation("Notification from the scheduler ({Kind}): {Title}", notification.Kind, notification.Title);
        stack.Show(notification, TimeSpan.FromSeconds(Math.Max(3, seconds)));
    }
}
