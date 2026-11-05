namespace TMPublisher.Desktop.ViewModels;

/// <summary>Lets a page open another page (the calendar opens a post in the compose screen) without knowing the shell.</summary>
public sealed class PageNavigator
{
    /// <summary>A page asked to be shown.</summary>
    public event EventHandler<PageViewModelBase>? Requested;

    /// <summary>Shows <paramref name="page"/>.</summary>
    public void Open(PageViewModelBase page) => Requested?.Invoke(this, page);
}
