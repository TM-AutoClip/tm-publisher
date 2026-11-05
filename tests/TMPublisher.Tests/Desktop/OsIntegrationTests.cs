using Shouldly;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Startup;
using TMPublisher.Runtime.Paths;

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
            arguments.ShouldBe(["--data-dir", folder]);

            WindowsAutostart.Command(@"C:\Program Files\TM Publisher\TMPublisherScheduler.exe", arguments)
                .ShouldBe("\"C:\\Program Files\\TM Publisher\\TMPublisherScheduler.exe\" --data-dir \"" + folder + "\"");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AutostartCommandsAreQuotedForEachSystem()
    {
        WindowsAutostart.Command(@"C:\a.exe", ["--data-dir", @"D:\"]).ShouldBe(@"""C:\a.exe"" --data-dir ""D:\\""");
        LinuxAutostart.Exec("/opt/tm publisher/tm-publisher-scheduler", ["--data-dir", "/home/u/my $data 100%"])
            .ShouldBe(@"""/opt/tm publisher/tm-publisher-scheduler"" --data-dir ""/home/u/my \\$data 100%%""");
        LinuxAutostart.Exec("/opt/tm/tm-publisher-scheduler", []).ShouldBe("\"/opt/tm/tm-publisher-scheduler\"");
    }

    [Fact]
    public void OnlyTheOldEntryOfTheSameDataFolderIsMoved()
    {
        var folder = Path.Combine(Path.GetTempPath(), "tmpublisher-tests", "own " + Guid.NewGuid().ToString("N"));
        try
        {
            var own = new AppPaths(folder);
            AutostartArguments.IsOldEntryOf(own, "\"C:\\x\\TMPublisher.exe\" --tray --data-dir \"" + folder + "\"").ShouldBeTrue();
            AutostartArguments.IsOldEntryOf(own, "\"C:\\x\\TMPublisher.exe\" --tray").ShouldBeFalse();
            AutostartArguments.IsOldEntryOf(own, "\"C:\\x\\TMPublisher.exe\" --tray --data-dir \"D:\\other\"").ShouldBeFalse();
            AutostartArguments.IsOldEntryOf(own, "\"C:\\x\\TMPublisherScheduler.exe\" --data-dir \"" + folder + "\"").ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void PickerPatternsAlsoMatchCapitals() =>
        FileDialogs.AnyCase(["*.mp4", "*.jpg"]).ShouldBe(["*.mp4", "*.MP4", "*.jpg", "*.JPG"]);
}
