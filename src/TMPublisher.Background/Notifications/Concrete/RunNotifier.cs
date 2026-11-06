using Microsoft.Extensions.Logging;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Settings;
using TMPublisher.Runtime.Notifications;

namespace TMPublisher.Background.Notifications;

/// <summary>
/// The <see cref="INotifier"/> of a run: every notification is logged, and shown in the bottom-right corner through the short-lived
/// notification process (<see cref="NotifyProcess"/>) when the user's settings allow it (on/off and how long; read once per run; when the
/// database cannot be read, as by a master-password run, shown for the default time).
/// </summary>
public sealed class RunNotifier(ISettingsStore settings, NotifyProcess process, ILogger<RunNotifier> logger) : INotifier
{
    private AppSettings? _settings;
    private bool _settingsRead;

    /// <inheritdoc />
    public void Notify(AppNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        logger.LogInformation("Notification ({Kind}): {Title}", notification.Kind, notification.Title);
        try
        {
            var current = Settings();
            if (!current.NotificationsEnabled)
            {
                return;
            }

            process.Show(new NotificationMessage(notification.Kind, notification.Title, notification.Message, Math.Max(3, current.NotificationSeconds)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The notification '{Title}' could not be shown", notification.Title);
        }
    }

    private AppSettings Settings()
    {
        if (!_settingsRead)
        {
            _settingsRead = true;
            try
            {
                _settings = settings.GetAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logger.LogInformation("The notification settings could not be read ({Type}); the default is used", ex.GetType().Name);
            }
        }

        return _settings ?? new AppSettings();
    }
}
