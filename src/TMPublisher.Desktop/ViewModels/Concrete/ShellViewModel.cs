using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Localization;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>The main window: sidebar of pages and the current page.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly Localizer _text;
    private readonly ILogger<ShellViewModel> _logger;

    /// <summary>Builds the sidebar from every registered page.</summary>
    public ShellViewModel(IEnumerable<PageViewModelBase> pages, Localizer text, ILogger<ShellViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        _logger = logger;
        Items = new ObservableCollection<NavItemViewModel>(pages.OrderBy(p => p.Order).Select(p => new NavItemViewModel(this, p, text[p.TitleKey])));
        foreach (var item in Items)
        {
            item.Page.NavigationRequested += (_, page) => _ = NavigateAsync(page);
        }
    }

    /// <summary>Sidebar.</summary>
    public ObservableCollection<NavItemViewModel> Items { get; }

    /// <summary>Shown page.</summary>
    [ObservableProperty]
    public partial PageViewModelBase? Current { get; set; }

    /// <summary>App-wide status line (startup problems).</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    /// <summary>Opens the first page.</summary>
    public Task StartAsync() => Items.Count > 0 ? NavigateAsync(Items[0].Page) : Task.CompletedTask;

    /// <summary>Opens a page of the given type (other pages link to each other).</summary>
    public Task NavigateAsync<TPage>()
        where TPage : PageViewModelBase =>
        Items.FirstOrDefault(i => i.Page is TPage) is { } item ? NavigateAsync(item.Page) : Task.CompletedTask;

    /// <summary>Opens a page; a failing reload is shown on the page and logged.</summary>
    public async Task NavigateAsync(PageViewModelBase page)
    {
        ArgumentNullException.ThrowIfNull(page);
        foreach (var item in Items)
        {
            item.IsSelected = item.Page == page;
        }

        Current = page;
        try
        {
            await page.ActivateAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Page {Page} failed to load", page.GetType().Name);
            page.Status = _text.Format("page.load_failed", ex.Message);
        }
    }
}
