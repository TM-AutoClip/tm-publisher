using Shouldly;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Text;

namespace TMPublisher.Tests.Core;

/// <summary>"What is published = what the form shows": the composer is the only place that merges tags, once, in the form.</summary>
public sealed class PostComposerTests
{
    [Fact]
    public void PlatformWithoutTitleGetsTitleOnFirstLine()
    {
        var (text, title, keywords) = PostComposer.Compose(TestPlatform.Caps(PostKind.Text), "T", "B", ["one"], ["two", "#one"]);
        text.ShouldBe("T\n\nB\n\n#one #two");
        title.ShouldBeEmpty();
        keywords.ShouldBeEmpty();
    }

    [Fact]
    public void KeywordPlatformKeepsTagsOutOfTheText()
    {
        var caps = TestPlatform.Caps(PostKind.Video) with { HasTitle = true, HasKeywords = true };
        var (text, title, keywords) = PostComposer.Compose(caps, "T", "B", ["#shorts"], ["Game", "game"]);
        text.ShouldBe("B");
        title.ShouldBe("T");
        keywords.ShouldBe(["shorts", "Game"]);
    }
}
