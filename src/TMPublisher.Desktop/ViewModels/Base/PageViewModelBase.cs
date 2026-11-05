using CommunityToolkit.Mvvm.ComponentModel;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// A page of the main window. Each page also implements <see cref="IPageInfo"/> (title, glyph, sidebar position) and is registered with
/// <c>AddPage</c>; it is created the first time it is shown and then kept, so unfinished input survives closing the window to the tray.
/// </summary>
public abstract partial class PageViewModelBase : ObservableObject
{
    /// <summary>Status line of the page.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    /// <summary>Called whenever the page is shown (reload data here).</summary>
    public virtual Task ActivateAsync() => Task.CompletedTask;
}
