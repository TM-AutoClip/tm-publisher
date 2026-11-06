using CommunityToolkit.Mvvm.ComponentModel;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// A page of the main window. Each page also implements <see cref="IPageInfo"/> (title, glyph, sidebar position) and is registered with
/// <c>AddPage</c>; it is created the first time it is shown and then kept while the window is open. Closing the window ends the window
/// program (the system scheduler sends while it is closed), so a page with unsaved input says so (<see cref="HasUnsavedWork"/>).
/// </summary>
public abstract partial class PageViewModelBase : ObservableObject
{
    /// <summary>Status line of the page.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    /// <summary>Whether the page holds input that is lost when the window closes.</summary>
    public virtual bool HasUnsavedWork => false;

    /// <summary>Called whenever the page is shown (reload data here).</summary>
    public virtual Task ActivateAsync() => Task.CompletedTask;
}
