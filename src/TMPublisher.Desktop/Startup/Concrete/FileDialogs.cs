using Avalonia.Platform.Storage;
using TMPublisher.Desktop.Localization;

namespace TMPublisher.Desktop.Startup;

/// <summary>Open / save file pickers of the active window.</summary>
public sealed class FileDialogs(WindowHost host, Localizer text)
{
    /// <summary>Asks for a file to open; <c>null</c> when canceled.</summary>
    public async Task<string?> OpenAsync(string titleKey, string filterKey, IReadOnlyList<string> patterns, bool multiple = false)
    {
        var all = await OpenManyAsync(titleKey, filterKey, patterns, multiple).ConfigureAwait(true);
        return all.Count > 0 ? all[0] : null;
    }

    /// <summary>Asks for files to open.</summary>
    public async Task<IReadOnlyList<string>> OpenManyAsync(string titleKey, string filterKey, IReadOnlyList<string> patterns, bool multiple)
    {
        if (host.Active is not { } window)
        {
            return [];
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = text[titleKey],
            AllowMultiple = multiple,
            FileTypeFilter = [new FilePickerFileType(text[filterKey]) { Patterns = AnyCase(patterns) }, new FilePickerFileType(text["filter.all_files"]) { Patterns = ["*"] }],
        }).ConfigureAwait(true);
        return files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
    }

    /// <summary>
    /// Each pattern also in capitals: Linux pickers (GTK, portals) match case-sensitively, so <c>*.mp4</c> alone hides VIDEO.MP4 from a phone.
    /// Other mixes are reachable through the "All files" choice; what is picked is still checked by its extension.
    /// </summary>
    internal static IReadOnlyList<string> AnyCase(IReadOnlyList<string> patterns) =>
        [.. patterns.SelectMany(p => new[] { p, p.ToUpperInvariant() }).Distinct(StringComparer.Ordinal)];

    /// <summary>Asks where to save; <c>null</c> when canceled. The extension is added when missing.</summary>
    public async Task<string?> SaveAsync(string titleKey, string suggestedName, string filterKey, string extension)
    {
        if (host.Active is not { } window)
        {
            return null;
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = text[titleKey],
            SuggestedFileName = suggestedName + extension,
            DefaultExtension = extension.TrimStart('.'),
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType(text[filterKey]) { Patterns = ["*" + extension] }],
        }).ConfigureAwait(true);
        if (file?.TryGetLocalPath() is not { Length: > 0 } path)
        {
            return null;
        }

        return path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? path : path + extension;
    }
}
