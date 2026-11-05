using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Startup;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Desktop.Views;

namespace TMPublisher.Tests.Desktop;

/// <summary>The main window is released when it goes to the tray and made again when opened; pages are made on first use.</summary>
public sealed class MainWindowHostTests
{
    [AvaloniaFact]
    public void ClosingWithATrayReleasesTheWindowAndOpeningMakesANewOne()
    {
        using var h = new DesktopHarness();
        var shell = h.Services.GetRequiredService<ShellViewModel>();
        var host = h.Services.GetRequiredService<MainWindowHost>();
        var windows = h.Services.GetRequiredService<WindowHost>();
        var made = 0;
        var released = 0;
        Window? lifetimeMain = null;
        host.Configure(() =>
        {
            made++;
            return new MainWindow { DataContext = shell };
        }, () => true, w => lifetimeMain = w);
        host.Released += (_, _) => released++;

        var closed = ShowAndClose(host, windows, out var shownWindow);
        shownWindow.ShouldBeTrue();
        Dispatcher.UIThread.RunJobs();

        host.Window.ShouldBeNull();
        windows.Active.ShouldBeNull();
        lifetimeMain.ShouldBeNull();
        released.ShouldBe(1);

        // Nothing (the singleton shell and its sidebar collection, the dialog owner, the lifetime) keeps the closed window alive.
        for (var i = 0; i < 10 && closed.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Dispatcher.UIThread.RunJobs();
        }

        closed.IsAlive.ShouldBeFalse();

        var again = host.Show();
        made.ShouldBe(2);
        again.IsVisible.ShouldBeTrue();
        again.DataContext.ShouldBeSameAs(shell);
        lifetimeMain.ShouldBeSameAs(again);
        windows.Active.ShouldBeSameAs(again);
        host.BeginQuit();
        again.Close();
    }

    [AvaloniaFact]
    public void WithoutATrayClosingMinimizesAndKeepsTheWindow()
    {
        using var h = new DesktopHarness();
        var host = h.Services.GetRequiredService<MainWindowHost>();
        var released = 0;
        host.Configure(() => new Window(), () => false, _ => { });
        host.Released += (_, _) => released++;

        var window = host.Show();
        window.Close();
        Dispatcher.UIThread.RunJobs();

        host.Window.ShouldBeSameAs(window);
        window.IsVisible.ShouldBeTrue();
        window.WindowState.ShouldBe(WindowState.Minimized);
        released.ShouldBe(0);

        host.Show().ShouldBeSameAs(window);
        window.WindowState.ShouldBe(WindowState.Normal);
        host.BeginQuit();
        window.Close();
        host.Window.ShouldBeNull();
    }

    [AvaloniaFact]
    public void QuitClosesWithoutRefusingOrReleasingToTheTray()
    {
        using var h = new DesktopHarness();
        var host = h.Services.GetRequiredService<MainWindowHost>();
        var released = 0;
        host.Configure(() => new Window(), () => false, _ => { });
        host.Released += (_, _) => released++;

        var window = host.Show();
        host.BeginQuit();
        window.Close();
        Dispatcher.UIThread.RunJobs();

        window.IsVisible.ShouldBeFalse();
        host.Window.ShouldBeNull();
        released.ShouldBe(0);
    }

    [AvaloniaFact]
    public void PagesAreMadeOnlyWhenFirstOpened()
    {
        using var h = new DesktopHarness();
        var made = new List<Type>();
        var entries = h.Services.GetServices<PageEntry>().Select(e => e with
        {
            Create = () =>
            {
                made.Add(e.PageType);
                return e.Create();
            },
        }).ToList();

        var shell = new ShellViewModel(entries, h.Services.GetRequiredService<PageNavigator>(), h.Services.GetRequiredService<Localizer>(),
            NullLogger<ShellViewModel>.Instance);

        shell.Items.Count.ShouldBe(8);
        shell.Items.Select(i => i.Entry.Order).ShouldBeInOrder();
        shell.Items.ShouldAllBe(i => i.Title.Length > 0 && i.Glyph.Length > 0);
        shell.IsStarted.ShouldBeFalse();
        made.ShouldBeEmpty();

        var tags = shell.Items.Single(i => i.Entry.PageType == typeof(TagSetsViewModel));
        tags.Page.ShouldBeSameAs(tags.Page);
        tags.Page.ShouldBeOfType<TagSetsViewModel>();
        made.Distinct().ShouldBe([typeof(TagSetsViewModel)]);
    }

    /// <summary>Shows and closes in its own frame, so no local of the test keeps the window alive.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ShowAndClose(MainWindowHost host, WindowHost windows, out bool wasShown)
    {
        var window = host.Show();
        wasShown = window.IsVisible && windows.Active == window && host.Window == window;
        window.Close();
        return new WeakReference(window);
    }
}
