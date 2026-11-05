using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Desktop.Os;

/// <summary>Opens addresses in the default browser (the shell picks it: Windows file association, xdg-open on Linux).</summary>
public sealed class BrowserLauncher(ILogger<BrowserLauncher> logger)
{
    /// <summary>Opens the address; logs why when it cannot (the address may carry a client id, so only the host is logged).</summary>
    public void Open(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.LogError(ex, "The browser could not be opened for {Host}", address.Host);
            throw;
        }
    }

    /// <summary>Opens the address for a button; <c>false</c> (logged with the reason) when no browser could be started.</summary>
    public bool TryOpen(Uri address)
    {
        try
        {
            Open(address);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
