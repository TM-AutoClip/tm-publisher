using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Desktop.Os;

/// <summary>Linux: <c>xdg-open</c> opens the file's folder in the desktop's file manager.</summary>
public sealed class LinuxFileReveal(ILogger<LinuxFileReveal> logger) : IFileReveal
{
    /// <inheritdoc />
    public void Reveal(string file)
    {
        var folder = Path.GetDirectoryName(file) ?? file;
        var start = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
        start.ArgumentList.Add(folder);
        using var _ = Process.Start(start);
        logger.LogInformation("The file manager was asked to open the reports folder");
    }
}
