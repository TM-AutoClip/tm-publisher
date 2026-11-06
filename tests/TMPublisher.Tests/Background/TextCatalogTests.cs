using Shouldly;
using TMPublisher.Background.Localization;

namespace TMPublisher.Tests.Background;

/// <summary>A run's texts: the user's language, English for a missing key, the key itself as the last resort, invariant formatting.</summary>
public sealed class TextCatalogTests
{
    [Fact]
    public void TextsFollowTheChosenLanguage()
    {
        var text = new TextCatalog();
        text["notify.unlock_title"].ShouldBe("Open TM Publisher to send");
        text.Use("tr");
        text.Language.ShouldBe("tr");
        text["notify.unlock_title"].ShouldBe("Göndermek için TM Publisher'ı açın");
        text.Format("notify.missed_text", 1234).ShouldContain("1234");
        text["no.such.key"].ShouldBe("no.such.key");
        text.Use("xx");
        text.Language.ShouldBe("en");
    }
}
