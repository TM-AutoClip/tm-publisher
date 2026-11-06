using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Localization;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>The main window: sidebar of pages and the current page.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly Localizer _text;
    private readonly ILogger<ShellViewModel> _logger;
    private readonly TimeProvider _clock;
    private readonly Func<ReportProblemViewModel> _report;
    private readonly List<PageViewModelBase> _opened = [];
    private DateTimeOffset? _warnedAt;

    /// <summary>Builds the sidebar from every registered page (the pages themselves are created when first opened).</summary>
    public ShellViewModel(IEnumerable<PageEntry> pages, PageNavigator navigator, Localizer text, TimeProvider clock, Func<ReportProblemViewModel> report,
        ILogger<ShellViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        _logger = logger;
        _clock = clock;
        _report = report;
        Items = new ObservableCollection<NavItemViewModel>(pages.OrderBy(p => p.Order).Select(p => new NavItemViewModel(this, p, text[p.TitleKey])));
        navigator.Requested += (_, page) => _ = NavigateAsync(page);
    }

    /// <summary>Sidebar.</summary>
    public ObservableCollection<NavItemViewModel> Items { get; }

    /// <summary>Shown page.</summary>
    [ObservableProperty]
    public partial PageViewModelBase? Current { get; set; }

    /// <summary>App-wide status line (startup problems).</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    /// <summary>Shows the "Report a problem" dialog (set by the main window).</summary>
    public Func<ReportProblemViewModel, Task>? ShowReportProblem { get; set; }

    /// <summary>The user chose "Close" in the window (the program ends; the system scheduler sends while it is closed).</summary>
    public event EventHandler? QuitRequested;

    /// <summary>Opens the "Report a problem" dialog (nothing is sent from there by the program).</summary>
    [RelayCommand]
    private async Task ReportProblemAsync()
    {
        _logger.LogInformation("Report a problem opened");
        if (ShowReportProblem is { } show)
        {
            await show(_report()).ConfigureAwait(true);
        }
    }

    /// <summary>Closes the window program.</summary>
    [RelayCommand]
    private void Quit()
    {
        _logger.LogInformation("Close chosen in the main window");
        QuitRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Whether the window may close now: when an opened page holds unsaved input, the first close is refused once, that page is shown with a
    /// note, and a second close within a short time goes through (the input is then discarded).
    /// </summary>
    public bool ConfirmClose()
    {
        if (_opened.FirstOrDefault(p => p.HasUnsavedWork) is not { } page)
        {
            return true;
        }

        var now = _clock.GetUtcNow();
        if (_warnedAt is { } warned && now - warned < TimeSpan.FromSeconds(30))
        {
            _logger.LogInformation("Window closed with unsaved input on {Page} after the note", page.GetType().Name);
            return true;
        }

        _warnedAt = now;
        page.Status = _text["window.unsaved_close"];
        _ = NavigateAsync(page);
        return false;
    }

    /// <summary>Whether a page has been opened (the window was shown at least once).</summary>
    public bool IsStarted => Current is not null;

    /// <summary>Opens the first page.</summary>
    public Task StartAsync() => Items.Count > 0 ? NavigateAsync(Items[0].Page) : Task.CompletedTask;

    /// <summary>Opens a page of the given type (other pages link to each other).</summary>
    public Task NavigateAsync<TPage>()
        where TPage : PageViewModelBase =>
        Items.FirstOrDefault(i => i.Entry.PageType == typeof(TPage)) is { } item ? NavigateAsync(item.Page) : Task.CompletedTask;

    /// <summary>Opens a page; a failing reload is shown on the page and logged.</summary>
    public async Task NavigateAsync(PageViewModelBase page)
    {
        ArgumentNullException.ThrowIfNull(page);
        foreach (var item in Items)
        {
            item.IsSelected = item.Entry.PageType == page.GetType();
        }

        Current = page;
        if (!_opened.Contains(page))
        {
            _opened.Add(page);
        }

        try
        {
            await page.ActivateAsync().ConfigureAwait(true);
            _logger.LogInformation("Page {Page} opened", page.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Page {Page} failed to load", page.GetType().Name);
            page.Status = _text.Format("page.load_failed", ex.Message);
        }
    }
}
