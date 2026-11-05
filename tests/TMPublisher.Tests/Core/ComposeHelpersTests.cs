using Shouldly;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Text;

namespace TMPublisher.Tests.Core;

public sealed class ComposeHelpersTests
{
    [Fact]
    public void KindFollowsTheFiles()
    {
        MediaFiles.KindOf([]).ShouldBe(PostKind.Text);
        MediaFiles.KindOf([new("a.jpg", MediaType.Photo, 1), new("b.png", MediaType.Photo, 1)]).ShouldBe(PostKind.Photo);
        MediaFiles.KindOf([new("a.mp4", MediaType.Video, 1)]).ShouldBe(PostKind.Video);
        MediaFiles.KindOf([new("a.mp4", MediaType.Video, 1), new("b.jpg", MediaType.Photo, 1)]).ShouldBe(PostKind.Album);
        MediaFiles.TypeOf("x.MOV").ShouldBe(MediaType.Video);
        MediaFiles.TypeOf("x.txt").ShouldBeNull();
    }

    [Fact]
    public void TagLineSplitsAndRemovesDuplicates()
    {
        TagLine.Parse("#a b, c;#B  a").ShouldBe(["#a", "b", "c"]);
        TagLine.Join(["#a", "b"]).ShouldBe("#a b");
    }

    [Fact]
    public void CostUsesTheLinkPriceForLinks()
    {
        var caps = new PlatformCapabilities
        {
            Kinds = new HashSet<PostKind> { PostKind.Text },
            MaxTextLength = 280,
            TextMeasure = Utf16Measure.Instance,
            CostPerPost = 0.015m,
            CostPerLinkPost = 0.20m,
        };
        CostEstimator.Estimate(caps, "hello").ShouldBe(0.015m);
        CostEstimator.Estimate(caps, "see https://example.com").ShouldBe(0.20m);
    }
}
