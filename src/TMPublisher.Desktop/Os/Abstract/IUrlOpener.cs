namespace TMPublisher.Desktop.Os;

/// <summary>Opens an address in the default browser (tests give a fake: no test opens a browser).</summary>
public interface IUrlOpener
{
    /// <summary>Opens <paramref name="address"/>; <c>false</c> (logged with the reason) when no browser could be started.</summary>
    bool TryOpen(Uri address);
}
