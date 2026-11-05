using System.Globalization;
using TMPublisher.Core.Localization;
using TMPublisher.Runtime.Localization;

namespace TMPublisher.Background.Localization;

/// <summary>
/// The scheduler's texts (notifications, the reasons stored on posts, the tray menu) in the language chosen in the window. The language files
/// are read the first time a text is needed. Arguments are formatted with the invariant culture: the scheduler runs without ICU, and only
/// names, counts and short reasons are put into its texts.
/// </summary>
public sealed class TextCatalog : ILocalizer
{
    private Dictionary<string, string>? _english;
    private Dictionary<string, string>? _current;

    /// <inheritdoc />
    public string Language { get; private set; } = "en";

    /// <inheritdoc />
    public string this[string key]
    {
        get
        {
            var english = _english ??= LanguageFiles.Load("en");
            var current = Language == "en" ? english : _current ??= LanguageFiles.Load(Language);
            return current.TryGetValue(key, out var value) ? value : english.GetValueOrDefault(key, key);
        }
    }

    /// <summary>Switches the language (unknown codes mean English).</summary>
    public void Use(string? language)
    {
        var chosen = language is not null && LanguageFiles.Supported.Contains(language) ? language : "en";
        if (chosen != Language)
        {
            Language = chosen;
            _current = null;
        }
    }

    /// <inheritdoc />
    public string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, this[key], args);
}
