using Shouldly;
using TMPublisher.Desktop.Startup;

namespace TMPublisher.Tests.Desktop;

/// <summary>The file picker patterns (the system scheduler's definitions are in Background/TriggerTests).</summary>
public sealed class OsIntegrationTests
{
    [Fact]
    public void PickerPatternsAlsoMatchCapitals() =>
        FileDialogs.AnyCase(["*.mp4", "*.jpg"]).ShouldBe(["*.mp4", "*.MP4", "*.jpg", "*.JPG"]);
}
