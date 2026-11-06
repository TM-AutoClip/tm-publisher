using Shouldly;
using TMPublisher.Desktop.Paths;

namespace TMPublisher.Tests.Desktop;

public sealed class StartOptionsTests
{
    [Fact]
    public void TheProcessorDrawsUnlessGpuIsAsked()
    {
        var plain = StartOptions.Parse([]);
        plain.RunDue.ShouldBeFalse();
        if (Environment.GetEnvironmentVariable("TMPUBLISHER_GPU") != "1")
        {
            plain.Gpu.ShouldBeFalse();
        }

        StartOptions.Parse(["--gpu"]).Gpu.ShouldBeTrue();
        StartOptions.Parse(["--gpu"]).RunDue.ShouldBeFalse();
    }

    [Fact]
    public void TheRunAndTheOldTrayStartRunWithoutAWindow()
    {
        StartOptions.Parse(["--run-due", "--data-dir", "x"]).RunDue.ShouldBeTrue();
        StartOptions.Parse(["--tray"]).RunDue.ShouldBeTrue();
        StartOptions.Parse(["--run-due", "--data-dir", "x"]).DataDirectory.ShouldBe(Path.GetFullPath("x"));
    }

    [Fact]
    public void NotifyModeIsRecognised()
    {
        StartOptions.Parse(["--notify", "--data-dir", "x"]).Notify.ShouldBeTrue();
        StartOptions.Parse([]).Notify.ShouldBeFalse();
    }
}
