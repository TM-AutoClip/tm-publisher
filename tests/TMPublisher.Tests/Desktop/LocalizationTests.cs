using Shouldly;
using TMPublisher.Desktop.Localization;

namespace TMPublisher.Tests.Desktop;

/// <summary>Both language files have exactly the same keys, and every key used in the views exists.</summary>
public sealed class LocalizationTests
{
    [Fact]
    public void EnglishAndTurkishHaveTheSameKeys()
    {
        var en = Localizer.Keys("en").ToHashSet(StringComparer.Ordinal);
        var tr = Localizer.Keys("tr").ToHashSet(StringComparer.Ordinal);
        en.Except(tr).ShouldBeEmpty("missing in tr.json");
        tr.Except(en).ShouldBeEmpty("missing in en.json");
    }

    [Fact]
    public void KeysUsedInViewsExist()
    {
        var en = Localizer.Keys("en").ToHashSet(StringComparer.Ordinal);
        var used = Directory.EnumerateFiles(Path.Combine(TMPublisher.Tests.Architecture.RepositoryFiles.Root, "src", "TMPublisher.Desktop"), "*.axaml", SearchOption.AllDirectories)
            .SelectMany(f => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"\{l:Tr ([\w.]+)\}").Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.Ordinal);
        used.Where(k => !en.Contains(k)).ShouldBeEmpty();
    }

    [Fact]
    public void KeysWrittenInCodeExist()
    {
        var en = Localizer.Keys("en").ToHashSet(StringComparer.Ordinal);
        var prefixes = string.Join('|', en.Select(k => k.Split('.')[0]).Distinct(StringComparer.Ordinal).Select(System.Text.RegularExpressions.Regex.Escape));
        var pattern = new System.Text.RegularExpressions.Regex("\"((?:" + prefixes + ")\\.[a-z0-9_.]*[a-z0-9_])\"");
        var used = Directory.EnumerateFiles(Path.Combine(TMPublisher.Tests.Architecture.RepositoryFiles.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .SelectMany(f => pattern.Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.Ordinal);
        used.Where(k => !en.Contains(k)).ShouldBeEmpty();
    }
}
