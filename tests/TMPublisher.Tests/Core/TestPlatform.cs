using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;

namespace TMPublisher.Tests.Core;

/// <summary>A configurable platform for rule tests (no connection, no publisher).</summary>
internal sealed class TestPlatform(PlatformCapabilities capabilities, string code = "test") : IPlatform
{
    public string Code => code;

    public string DisplayName => "Test";

    public string Color => "#000000";

    public PlatformCapabilities Capabilities => capabilities;

    public IConnectionMethod Connection => throw new NotSupportedException();

    public SetupGuide Guide => new([], []);

    public IPublisher Publisher => throw new NotSupportedException();

    public static PlatformCapabilities Caps(params PostKind[] kinds) => new()
    {
        Kinds = kinds.ToHashSet(),
        MaxTextLength = 10,
        TextMeasure = GraphemeMeasure.Instance,
        MaxPhotos = 2,
        MaxPhotoBytes = 100,
        MaxHashtags = 1,
    };
}
