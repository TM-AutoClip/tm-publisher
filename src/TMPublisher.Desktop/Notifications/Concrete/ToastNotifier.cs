using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Notifications;
using TMPublisher.Desktop.Startup;
using TMPublisher.Desktop.Views;

namespace TMPublisher.Desktop.Notifications;

/// <summary>
/// Bottom-right notifications in the app's own small window (decision: works the same on Windows and on every Linux desktop without
/// AUMID registration, D-Bus notification daemons or their own time limits; the duration and on/off are fully ours). Up to three are stacked;
/// each closes after the user's duration (default one minute) or with its close button. Off in Settings means nothing is shown.
/// </summary>
public sealed class ToastNotifier(AppSession session, ILogger<ToastNotifier> logger) : INotifier
{
    private const int MaxVisible = 3;
    private readonly List<ToastWindow> _open = [];

    /// <summary>Open windows (tests).</summary>
    internal IReadOnlyList<ToastWindow> Open => _open;

    /// <inheritdoc />
    public void Notify(AppNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        logger.LogInformation("Notification ({Kind}): {Title}", notification.Kind, notification.Title);
        if (!session.Settings.NotificationsEnabled)
        {
            return;
        }

        var duration = TimeSpan.FromSeconds(Math.Max(3, session.Settings.NotificationSeconds));
        Dispatcher.UIThread.Post(() => Show(new ToastViewModel(notification, duration)));
    }

    private void Show(ToastViewModel model)
    {
        while (_open.Count >= MaxVisible)
        {
            _open[0].Close();
        }

        var window = new ToastWindow { DataContext = model };
        window.Closed += (_, _) =>
        {
            _open.Remove(window);
            Arrange();
        };
        _open.Add(window);
        window.Show();
        Arrange();
        DispatcherTimer.RunOnce(window.Close, model.Duration);
    }

    private void Arrange()
    {
        var offset = 0;
        for (var i = _open.Count - 1; i >= 0; i--)
        {
            offset += _open[i].PlaceAt(offset);
        }
    }
}
