using Microsoft.Extensions.Logging;
using TMPublisher.Background.Ipc;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Settings;
using TMPublisher.Runtime.Ipc;
using TMPublisher.Runtime.Notifications;

namespace TMPublisher.Background.Notifications;

/// <summary>
/// The scheduler's <see cref="INotifier"/>: follows the user's settings (on/off, how long), shows the notification in the open window when
/// there is one, otherwise through the short-lived notification process (<see cref="NotifyProcess"/>). Every notification is also logged.
/// </summary>
public sealed class NotificationRelay(ISettingsStore settings, WatcherHub watchers, NotifyProcess process, ILogger<NotificationRelay> logger) : INotifier
{
    /// <inheritdoc />
    public void Notify(AppNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        logger.LogInformation("Notification ({Kind}): {Title}", notification.Kind, notification.Title);
        _ = DeliverAsync(notification);
    }

    private async Task DeliverAsync(AppNotification notification)
    {
        try
        {
            var current = await settings.GetAsync(CancellationToken.None).ConfigureAwait(false);
            if (!current.NotificationsEnabled)
            {
                return;
            }

            var message = new NotificationMessage(notification.Kind, notification.Title, notification.Message, Math.Max(3, current.NotificationSeconds));
            if (watchers.HasWatchers && await watchers.BroadcastAsync(SchedulerChannel.NotifyLine(message)).ConfigureAwait(false) > 0)
            {
                return;
            }

            process.Show(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The notification '{Title}' could not be shown", notification.Title);
        }
    }
}
