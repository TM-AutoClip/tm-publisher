using System.Text.Json;

namespace TMPublisher.Runtime.Localization;

/// <summary>
/// The English and Turkish texts embedded in this library (<c>Lang/en.json</c>, <c>Lang/tr.json</c>), shared by the window and the background
/// scheduler (its notifications and the reasons it stores on posts are shown in the user's language).
/// </summary>
public static class LanguageFiles
{
    /// <summary>Supported language codes.</summary>
    public static IReadOnlyList<string> Supported { get; } = ["en", "tr"];

    /// <summary>The texts of a language; an exception when the language is not embedded.</summary>
    public static Dictionary<string, string> Load(string language)
    {
        using var stream = typeof(LanguageFiles).Assembly.GetManifestResourceStream($"TMPublisher.Lang.{language}.json")
            ?? throw new InvalidOperationException($"Language file '{language}' is not embedded.");
        return JsonSerializer.Deserialize(stream, RuntimeJsonContext.Default.DictionaryStringString) ?? [];
    }
}
