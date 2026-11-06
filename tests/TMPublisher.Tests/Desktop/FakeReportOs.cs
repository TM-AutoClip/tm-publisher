using TMPublisher.Desktop.Os;

namespace TMPublisher.Tests.Desktop;

/// <summary>Records what would be opened: no browser and no file manager is started from a test.</summary>
internal sealed class FakeReportOs : IUrlOpener, IFileReveal
{
    public List<Uri> Opened { get; } = [];

    public List<string> Revealed { get; } = [];

    public bool TryOpen(Uri address)
    {
        Opened.Add(address);
        return true;
    }

    public void Reveal(string file) => Revealed.Add(file);
}
