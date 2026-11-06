namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// What the sidebar shows for a page, known without creating it: pages are created on first navigation (a page that is not opened costs
/// nothing).
/// </summary>
public interface IPageInfo
{
    /// <summary>Localization key of the title.</summary>
    static abstract string TitleKey { get; }

    /// <summary>A single glyph shown before the title.</summary>
    static abstract string Glyph { get; }

    /// <summary>Position in the sidebar.</summary>
    static abstract int Order { get; }
}
