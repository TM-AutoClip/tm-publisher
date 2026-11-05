using TMPublisher.Core.Notifications;

namespace TMPublisher.Tests.Scheduling;

internal sealed class RecordingNotifier : INotifier
{
    public List<AppNotification> All { get; } = [];

    public void Notify(AppNotification notification)
    {
        lock (All)
        {
            All.Add(notification);
        }
    }
}
