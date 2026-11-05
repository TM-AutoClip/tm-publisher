using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>A sidebar entry. The page behind it is created the first time it is opened.</summary>
public sealed partial class NavItemViewModel(ShellViewModel shell, PageEntry entry, string title) : ObservableObject
{
    /// <summary>The registered page.</summary>
    public PageEntry Entry { get; } = entry;

    /// <summary>The page (created on first use, then the same instance).</summary>
    public PageViewModelBase Page => Entry.Create();

    /// <summary>Title in the current language.</summary>
    public string Title { get; } = title;

    /// <summary>Glyph.</summary>
    public string Glyph => Entry.Glyph;

    /// <summary>Whether this page is shown.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Opens the page.</summary>
    [RelayCommand]
    private Task OpenAsync() => shell.NavigateAsync(Page);
}
