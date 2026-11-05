using Shouldly;
using TMPublisher.Core.Text;

namespace TMPublisher.Tests.Core;

public sealed class HashtagRulesTests
{
    [Theory]
    [InlineData("my channel", "#mychannel")]
    [InlineData("gta-rp", "#gtarp")]
    [InlineData("#already", "#already")]
    [InlineData("---", "")]
    public void TagCleansWords(string word, string expected) => HashtagRules.Tag(word).ShouldBe(expected);

    [Fact]
    public void AppendSkipsTagsAlreadyInTheText() =>
        HashtagRules.AppendTags("Hello #News", ["news", "world"]).ShouldBe("Hello #News\n\n#world");

    [Fact]
    public void TitledTextPutsTitleFirst() =>
        HashtagRules.TitledText("Title", "Body", ["a"]).ShouldBe("Title\n\nBody\n\n#a");
}
