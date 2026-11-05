using Shouldly;
using TMPublisher.Core.Text;

namespace TMPublisher.Tests.Core;

public sealed class XTextTests
{
    [Theory]
    [InlineData("hello", 5)]
    [InlineData("çğış", 4)]
    [InlineData("日本", 4)]
    [InlineData("👍", 2)]
    [InlineData("👨‍👩‍👧", 2)]
    [InlineData("see https://example.com/a/very/long/path?x=1", 4 + 23)]
    [InlineData("example.com", 23)]
    public void CountsLikeX(string text, int expected) => XText.WeightedLength(text).ShouldBe(expected);

    [Theory]
    [InlineData("visit example.com today", true)]
    [InlineData("discord.gg", false)]
    [InlineData("discord.gg/abc", true)]
    [InlineData("no link here", false)]
    [InlineData("#tag.com", false)]
    public void FindsLinks(string text, bool expected) => XText.ContainsLink(text).ShouldBe(expected);
}
