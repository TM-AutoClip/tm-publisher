using TMPublisher.Desktop.Os;

namespace TMPublisher.Tests.Desktop;

/// <summary>Autostart that only remembers the value (tests never touch the real Run key or autostart folder).</summary>
internal sealed class FakeAutostart : IAutostart
{
    public bool IsEnabled { get; private set; }

    public void Set(bool enabled) => IsEnabled = enabled;
}
