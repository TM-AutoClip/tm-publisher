using Microsoft.Extensions.Logging;
using TMPublisher.Core.Notifications;
using TMPublisher.Desktop.Startup;
using TMPublisher.Desktop.Views;

namespace TMPublisher.Desktop.Notifications;

/// <summary>
/// The window's notifications (<see cref="ToastStack"/>), following the user's settings: off means nothing is shown, the duration is the
/// user's (default one minute). While the window is open it is the sender, so the notifications of sending come through here.
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
}
