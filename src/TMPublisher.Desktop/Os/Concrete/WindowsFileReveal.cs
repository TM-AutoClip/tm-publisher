using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Desktop.Os;

/// <summary>Windows: Explorer opens the folder with the file selected.</summary>
public sealed class WindowsFileReveal(ILogger<WindowsFileReveal> logger) : IFileReveal
{
    /// <inheritdoc />
    public void Reveal(string file)
    {
        using var _ = Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + file + "\"") { UseShellExecute = false });
        logger.LogInformation("Explorer opened on the report file");
    }
}
