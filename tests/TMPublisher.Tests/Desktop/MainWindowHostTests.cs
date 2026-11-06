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

/// <summary>Closing the main window ends the window program (unsaved input refuses the first close once); pages are made on first use.</summary>
public sealed class MainWindowHostTests
{
    [AvaloniaFact]
    public void ClosingTheWindowEndsTheWindowProgram()
    {
        using var h = new DesktopHarness();
        var shell = h.Services.GetRequiredService<ShellViewModel>();
        var host = h.Services.GetRequiredService<MainWindowHost>();
        var windows = h.Services.GetRequiredService<WindowHost>();
        var closed = 0;
        Window? lifetimeMain = null;
        host.Configure(() => new MainWindow { DataContext = shell }, () => true, w => lifetimeMain = w);
        host.Closed += (_, _) => closed++;

        var window = host.Show();
        lifetimeMain.ShouldBeSameAs(window);
        windows.Active.ShouldBeSameAs(window);
        window.Close();
        Dispatcher.UIThread.RunJobs();

        closed.ShouldBe(1);
        host.Window.ShouldBeNull();
        windows.Active.ShouldBeNull();
        lifetimeMain.ShouldBeNull();
    }

    [AvaloniaFact]
    public void UnsavedInputRefusesTheFirstCloseOnce()
    {
        using var h = new DesktopHarness();
        var shell = h.Services.GetRequiredService<ShellViewModel>();
        var host = h.Services.GetRequiredService<MainWindowHost>();
        var closed = 0;
        host.Configure(() => new MainWindow { DataContext = shell }, shell.ConfirmClose, _ => { });
        host.Closed += (_, _) => closed++;
        var compose = h.Services.GetRequiredService<ComposeViewModel>();
        shell.NavigateAsync(compose).GetAwaiter().GetResult();
        compose.Body = "Not saved yet";

        var window = host.Show();
        window.Close();
        Dispatcher.UIThread.RunJobs();
        closed.ShouldBe(0);
        window.IsVisible.ShouldBeTrue();
        compose.Status.ShouldNotBeEmpty();

        window.Close();
        Dispatcher.UIThread.RunJobs();
        closed.ShouldBe(1);
    }

    [AvaloniaFact]
    public void QuitIsNeverRefused()
    {
        using var h = new DesktopHarness();
        var host = h.Services.GetRequiredService<MainWindowHost>();
        host.Configure(() => new Window(), () => false, _ => { });

        var window = host.Show();
        host.BeginQuit();
        window.Close();
        Dispatcher.UIThread.RunJobs();

        window.IsVisible.ShouldBeFalse();
        host.Window.ShouldBeNull();
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

        var shell = new ShellViewModel(entries, h.Services.GetRequiredService<PageNavigator>(), h.Services.GetRequiredService<Localizer>(), TimeProvider.System,
            h.Services.GetRequiredService<Func<ReportProblemViewModel>>(),
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
}
