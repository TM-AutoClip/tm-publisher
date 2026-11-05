using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Settings;
using TMPublisher.Desktop.Notifications;
using TMPublisher.Desktop.Startup;

namespace TMPublisher.Tests.Desktop;

public sealed class ToastTests
{
    [AvaloniaFact]
    public void NotificationsShowStackAndRespectTheSwitch()
    {
        using var h = new DesktopHarness();
        var notifier = h.Services.GetRequiredService<ToastNotifier>();
        for (var i = 0; i < 4; i++)
        {
            notifier.Notify(new AppNotification(NotificationKind.Published, "Published " + i, "Example"));
        }

        Dispatcher.UIThread.RunJobs();
        notifier.Open.Count.ShouldBe(3);
        foreach (var window in notifier.Open.ToList())
        {
            window.Close();
        }

        notifier.Open.ShouldBeEmpty();
        h.Services.GetRequiredService<AppSession>().Settings = new AppSettings { NotificationsEnabled = false };
        notifier.Notify(new AppNotification(NotificationKind.Failed, "Failed", "Example"));
        Dispatcher.UIThread.RunJobs();
        notifier.Open.ShouldBeEmpty();
    }
}
