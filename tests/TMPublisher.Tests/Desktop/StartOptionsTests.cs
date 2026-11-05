using Shouldly;
using TMPublisher.Desktop.Paths;

namespace TMPublisher.Tests.Desktop;

public sealed class StartOptionsTests
{
    [Fact]
    public void TheProcessorDrawsUnlessGpuIsAsked()
    {
        var plain = StartOptions.Parse(["--tray"]);
        plain.Tray.ShouldBeTrue();
        if (Environment.GetEnvironmentVariable("TMPUBLISHER_GPU") != "1")
        {
            plain.Gpu.ShouldBeFalse();
        }

        StartOptions.Parse(["--gpu"]).Gpu.ShouldBeTrue();
        StartOptions.Parse(["--gpu"]).Tray.ShouldBeFalse();
    }

    [Fact]
    public void NotifyModeIsRecognised()
    {
        StartOptions.Parse(["--notify", "--data-dir", "x"]).Notify.ShouldBeTrue();
        StartOptions.Parse([]).Notify.ShouldBeFalse();
    }
}
