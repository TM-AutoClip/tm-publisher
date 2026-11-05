using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>A sidebar entry.</summary>
public sealed partial class NavItemViewModel(ShellViewModel shell, PageViewModelBase page, string title) : ObservableObject
{
    /// <summary>Page.</summary>
    public PageViewModelBase Page { get; } = page;

    /// <summary>Title in the current language.</summary>
    public string Title { get; } = title;

    /// <summary>Glyph.</summary>
    public string Glyph => Page.Glyph;

    /// <summary>Whether this page is shown.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Opens the page.</summary>
    [RelayCommand]
    private Task OpenAsync() => shell.NavigateAsync(Page);
}
