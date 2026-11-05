using Shouldly;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Validation;

namespace TMPublisher.Tests.Core;

public sealed class PostValidatorTests
{
    private static IReadOnlyList<ValidationProblem> Check(PlatformCapabilities caps, Post post, string text) =>
        PostValidator.Validate(new TestPlatform(caps), post, new PostTarget { Text = text }, _ => true);

    [Fact]
    public void UnsupportedKindIsBlockedWithItsReason()
    {
        var caps = TestPlatform.Caps(PostKind.Text) with
        {
            UnsupportedReasons = new Dictionary<PostKind, string> { [PostKind.Video] = "reason.video" },
        };
        Check(caps, new Post { Kind = PostKind.Video, Media = [new MediaItem("a.mp4", MediaType.Video, 1)] }, "x")
            .Single().Key.ShouldBe("reason.video");
    }

    [Fact]
    public void LongTextAndTooManyTagsAreBlocked()
    {
        var problems = Check(TestPlatform.Caps(PostKind.Text), new Post { Kind = PostKind.Text }, "#a #b long text");
        problems.Select(p => p.Key).ShouldBe(["problem.text_too_long", "problem.too_many_hashtags"]);
    }

    [Fact]
    public void PhotoLimitsAreChecked()
    {
        var post = new Post
        {
            Kind = PostKind.Photo,
            Media = [new MediaItem("a.jpg", MediaType.Photo, 50), new MediaItem("b.jpg", MediaType.Photo, 500), new MediaItem("c.jpg", MediaType.Photo, 5)],
        };
        Check(TestPlatform.Caps(PostKind.Photo), post, "ok").Select(p => p.Key).ShouldBe(["problem.too_many_photos", "problem.file_too_large"]);
    }

    [Fact]
    public void MissingFileIsReported()
    {
        var post = new Post { Kind = PostKind.Photo, Media = [new MediaItem("gone.jpg", MediaType.Photo, 5)] };
        PostValidator.Validate(new TestPlatform(TestPlatform.Caps(PostKind.Photo)), post, new PostTarget { Text = "ok" }, _ => false)
            .Single().Key.ShouldBe("problem.file_missing");
    }

    [Fact]
    public void ValidPostHasNoProblem() =>
        Check(TestPlatform.Caps(PostKind.Text), new Post { Kind = PostKind.Text }, "ok #a").ShouldBeEmpty();
}
