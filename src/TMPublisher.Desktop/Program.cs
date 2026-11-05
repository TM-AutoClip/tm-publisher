using Avalonia;

namespace TMPublisher.Desktop;

/// <summary>Entry point of the desktop app.</summary>
internal static class Program
{
    /// <summary>Starts Avalonia with the classic desktop lifetime.</summary>
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Avalonia app builder (also used by the designer).</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
