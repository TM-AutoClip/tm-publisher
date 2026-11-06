using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Desktop.Os;

/// <summary>Opens addresses in the default browser (the shell picks it: Windows file association, xdg-open on Linux).</summary>
public sealed class BrowserLauncher(ILogger<BrowserLauncher> logger) : IUrlOpener
{
    /// <summary>
    /// Opens the address; logs why when it cannot (the address may carry a client id, so only the host is logged, and a server of the user's
    /// own account as <see cref="PlatformHosts.Other"/>).
    /// </summary>
    public void Open(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.LogError(ex, "The browser could not be opened for {Host}", PlatformHosts.Display(address));
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
            // Open logged the error with its cause; the caller shows the address instead.
            return false;
        }
    }
}
