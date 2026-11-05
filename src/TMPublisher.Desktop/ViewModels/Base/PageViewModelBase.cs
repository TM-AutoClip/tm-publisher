using CommunityToolkit.Mvvm.ComponentModel;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>A page of the main window. The sidebar lists every registered page by <see cref="Order"/>.</summary>
public abstract partial class PageViewModelBase : ObservableObject
{
    /// <summary>Localization key of the title.</summary>
    public abstract string TitleKey { get; }

    /// <summary>A single glyph shown before the title.</summary>
    public abstract string Glyph { get; }

    /// <summary>Position in the sidebar.</summary>
    public abstract int Order { get; }

    /// <summary>Status line of the page.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    /// <summary>Raised when the page wants another page shown (the shell opens it).</summary>
    public event EventHandler<PageViewModelBase>? NavigationRequested;

    /// <summary>Called whenever the page is shown (reload data here).</summary>
    public virtual Task ActivateAsync() => Task.CompletedTask;

    /// <summary>Asks the shell to show another page.</summary>
    protected void RequestNavigation(PageViewModelBase page) => NavigationRequested?.Invoke(this, page);
}
