namespace TMPublisher.Desktop.ViewModels;

/// <summary>A sidebar entry registered for a page; <see cref="Create"/> gives the page (one instance, made on first call).</summary>
public sealed record PageEntry(Type PageType, string TitleKey, string Glyph, int Order, Func<PageViewModelBase> Create);
