using Avalonia.Threading;
using TMPublisher.Core.Notifications;
using TMPublisher.Desktop.Views;

namespace TMPublisher.Desktop.Notifications;

/// <summary>
/// The bottom-right notification windows (decision: the app's own small window, the same on Windows and every Linux desktop, without AUMID
/// registration, D-Bus notification daemons or their own time limits). Up to three are stacked; each closes after its duration or with its
/// close button. Used by the open window and by the short-lived <c>--notify</c> process of the background scheduler.
/// </summary>
public sealed class ToastStack
{
    private const int MaxVisible = 3;
    private readonly List<ToastWindow> _open = [];

    /// <summary>Open windows.</summary>
    internal IReadOnlyList<ToastWindow> Open => _open;

    /// <summary>Raised on the UI thread when the last open notification closed.</summary>
    public event EventHandler? Emptied;

    /// <summary>Shows a notification for <paramref name="duration"/> (from any thread).</summary>
    public void Show(AppNotification notification, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(notification);
        Dispatcher.UIThread.Post(() => ShowNow(new ToastViewModel(notification, duration)));
    }

    private void ShowNow(ToastViewModel model)
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
            if (_open.Count == 0)
            {
                Emptied?.Invoke(this, EventArgs.Empty);
            }
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
