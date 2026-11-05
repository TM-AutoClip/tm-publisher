using Avalonia.Markup.Xaml;

namespace TMPublisher.Desktop.Localization;

/// <summary>XAML text in the chosen language: <c>Text="{l:Tr settings.title}"</c>.</summary>
public sealed class Tr(string key) : MarkupExtension
{
    /// <summary>Key.</summary>
    public string Key { get; } = key;

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider) => Localizer.Current?[Key] ?? Key;
}
