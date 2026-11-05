using System.Globalization;
using System.Text.Json;
using TMPublisher.Core.Localization;
using TMPublisher.Desktop.Paths;

namespace TMPublisher.Desktop.Localization;

/// <summary>
/// English and Turkish texts from the embedded <c>Lang/en.json</c> and <c>Lang/tr.json</c>. A missing Turkish key falls back to English, a
/// missing English key shows the key (the parity test keeps both files complete). The language is chosen at start; changing it applies
/// after a restart.
/// </summary>
public sealed class Localizer : ILocalizer
{
    private static readonly string[] Languages = ["en", "tr"];
    private readonly Dictionary<string, string> _english = Load("en");
    private Dictionary<string, string> _current;

    /// <summary>Loads English.</summary>
    public Localizer()
    {
        _current = _english;
        Language = "en";
        Current = this;
    }

    /// <summary>The instance used by the XAML markup extension.</summary>
    public static Localizer Current { get; private set; } = null!;

    /// <inheritdoc />
    public string Language { get; private set; }

    /// <summary>Culture of the current language (dates, numbers).</summary>
    public CultureInfo Culture => CultureInfo.GetCultureInfo(Language == "tr" ? "tr-TR" : "en-US");

    /// <inheritdoc />
    public string this[string key] => _current.TryGetValue(key, out var value) ? value : _english.GetValueOrDefault(key, key);

    /// <summary>Supported language codes.</summary>
    public static IReadOnlyList<string> Supported => Languages;

    /// <summary>Switches the language (unknown codes mean English).</summary>
    public void Use(string? language)
    {
        Language = Languages.Contains(language) ? language! : "en";
        _current = Language == "en" ? _english : Load(Language);
    }

    /// <summary>The language matching the operating system (Turkish system → tr, otherwise en).</summary>
    public static string SystemLanguage() => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? "tr" : "en";

    /// <inheritdoc />
    public string Format(string key, params object[] args) => string.Format(Culture, this[key], args);

    /// <summary>All keys of a language file (tests).</summary>
    internal static IReadOnlyCollection<string> Keys(string language) => Load(language).Keys;

    private static Dictionary<string, string> Load(string language)
    {
        using var stream = typeof(Localizer).Assembly.GetManifestResourceStream($"TMPublisher.Lang.{language}.json")
            ?? throw new InvalidOperationException($"Language file '{language}' is not embedded.");
        return JsonSerializer.Deserialize(stream, DesktopJsonContext.Default.DictionaryStringString) ?? [];
    }
}
