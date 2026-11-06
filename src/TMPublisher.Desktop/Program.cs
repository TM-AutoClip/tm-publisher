using Avalonia;
using TMPublisher.Background.Hosting;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.Startup;

namespace TMPublisher.Desktop;

/// <summary>Entry point of the one program file.</summary>
internal static class Program
{
    /// <summary>
    /// Arguments (<see cref="StartOptions"/>): <c>--run-due</c> runs without a window (<see cref="RunEntry"/>: nothing of the window's UI
    /// library is loaded, so this method only reads the options before it branches), <c>--notify</c> shows only notifications, anything else
    /// opens the window (<see cref="WindowProgram"/>).
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        var options = StartOptions.Parse(args);
        return options.RunDue ? RunEntry.Run(options.DataDirectory) : WindowProgram.Run(options, args);
    }

    /// <summary>Avalonia app builder (used by the designer).</summary>
    public static AppBuilder BuildAvaloniaApp() => WindowProgram.BuildAvaloniaApp(gpu: false);
}
