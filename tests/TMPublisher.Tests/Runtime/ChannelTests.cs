using Shouldly;
using TMPublisher.Core.Notifications;
using TMPublisher.Runtime.Instance;
using TMPublisher.Runtime.Notifications;
using TMPublisher.Tests.Storage;

namespace TMPublisher.Tests.Runtime;

/// <summary>The notification lines of the notification process, and the per-folder instance locks.</summary>
public sealed class ChannelTests
{
    [Fact]
    public void NotificationsRoundTrip()
    {
        var message = new NotificationMessage(NotificationKind.Failed, "Başarısız", "X · hesap: \"quote\"\nline", 60);
        NotificationLines.Parse(NotificationLines.Write(message)).ShouldBe(message);
        NotificationLines.Write(message).ShouldNotContain("\n");
        NotificationLines.Parse("ok").ShouldBeNull();
    }

    [Fact]
    public void OnlyOneHolderPerDataFolder()
    {
        using var paths = new TempPaths();
        using (var first = InstanceLock.TryAcquire(paths.DataDirectory, InstanceLock.Sender, out _))
        {
            first.ShouldNotBeNull();
            InstanceLock.TryAcquire(paths.DataDirectory, InstanceLock.Sender, out var reason).ShouldBeNull();
            reason.ShouldNotBeEmpty();
            using var window = InstanceLock.TryAcquire(paths.DataDirectory, InstanceLock.Window, out _);
            window.ShouldNotBeNull();
        }

        using var again = InstanceLock.TryAcquire(paths.DataDirectory, InstanceLock.Sender, out _);
        again.ShouldNotBeNull();
    }
}
