using Shouldly;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.Startup;

namespace TMPublisher.Tests.Desktop;

/// <summary>Command lines written for the operating system and the file picker patterns (no registry, no autostart folder is touched).</summary>
public sealed class OsIntegrationTests
{
    [Fact]
    public void AutostartKeepsAnOwnDataFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "tmpublisher-tests", "own data " + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(folder);
            paths.IsDefaultDirectory.ShouldBeFalse();
            var arguments = AutostartArguments.For(paths);
            arguments.ShouldBe(["--tray", "--data-dir", folder]);

            WindowsAutostart.Command(@"C:\Program Files\TM Publisher\TMPublisher.exe", arguments)
                .ShouldBe("\"C:\\Program Files\\TM Publisher\\TMPublisher.exe\" --tray --data-dir \"" + folder + "\"");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AutostartCommandsAreQuotedForEachSystem()
    {
        WindowsAutostart.Command(@"C:\a.exe", ["--tray", "--data-dir", @"D:\"]).ShouldBe(@"""C:\a.exe"" --tray --data-dir ""D:\\""");
        LinuxAutostart.Exec("/opt/tm publisher/TMPublisher", ["--tray", "--data-dir", "/home/u/my $data 100%"])
            .ShouldBe(@"""/opt/tm publisher/TMPublisher"" --tray --data-dir ""/home/u/my \\$data 100%%""");
        LinuxAutostart.Exec("/opt/tm/TMPublisher", ["--tray"]).ShouldBe("\"/opt/tm/TMPublisher\" --tray");
    }

    [Fact]
    public void PickerPatternsAlsoMatchCapitals() =>
        FileDialogs.AnyCase(["*.mp4", "*.jpg"]).ShouldBe(["*.mp4", "*.MP4", "*.jpg", "*.JPG"]);
}
