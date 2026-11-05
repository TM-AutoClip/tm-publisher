using Shouldly;
using TMPublisher.Background.Localization;

namespace TMPublisher.Tests.Background;

/// <summary>The scheduler's texts: the user's language, English for a missing key, the key itself as the last resort, invariant formatting.</summary>
public sealed class TextCatalogTests
{
    [Fact]
    public void TextsFollowTheChosenLanguage()
    {
        var text = new TextCatalog();
        text["tray.open"].ShouldBe("Open TM Publisher");
        text.Use("tr");
        text.Language.ShouldBe("tr");
        text["tray.open"].ShouldBe("TM Publisher'ı aç");
        text.Format("notify.missed_text", 1234).ShouldContain("1234");
        text["no.such.key"].ShouldBe("no.such.key");
        text.Use("xx");
        text.Language.ShouldBe("en");
    }

    [Fact]
    public void EveryStateHasATextInBothLanguages()
    {
        foreach (var language in new[] { "en", "tr" })
        {
            var text = new TextCatalog();
            text.Use(language);
            foreach (var status in Enum.GetValues<TMPublisher.Runtime.Ipc.SchedulerStatus>())
            {
                var key = "scheduler.state." + status.ToString().ToLowerInvariant();
                text[key].ShouldNotBe(key);
            }
        }
    }
}
