using TMPublisher.Core.Connections;
using TMPublisher.Core.Publishing;

namespace TMPublisher.Core.Platforms;

/// <summary>
/// A social platform (keyed strategy). The list comes from DI: a new platform is a new class, no switch on platform codes anywhere.
/// </summary>
public interface IPlatform
{
    /// <summary>Code (<see cref="PlatformCodes"/>).</summary>
    string Code { get; }

    /// <summary>Display name.</summary>
    string DisplayName { get; }

    /// <summary>Brand color (#RRGGBB) for badges and calendar cards.</summary>
    string Color { get; }

    /// <summary>What it accepts.</summary>
    PlatformCapabilities Capabilities { get; }

    /// <summary>How an account is connected.</summary>
    IConnectionMethod Connection { get; }

    /// <summary>Setup guide.</summary>
    SetupGuide Guide { get; }

    /// <summary>Publishes a post to one account.</summary>
    IPublisher Publisher { get; }
}
