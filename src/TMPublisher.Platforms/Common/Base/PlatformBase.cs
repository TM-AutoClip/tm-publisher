using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Publishing;

namespace TMPublisher.Platforms.Common;

/// <summary>Common shape of a platform: the platform class declares what it is; connection and publisher are its own classes.</summary>
public abstract class PlatformBase : IPlatform
{
    /// <inheritdoc />
    public abstract string Code { get; }

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public abstract string Color { get; }

    /// <inheritdoc />
    public abstract PlatformCapabilities Capabilities { get; }

    /// <inheritdoc />
    public abstract IConnectionMethod Connection { get; }

    /// <inheritdoc />
    public abstract SetupGuide Guide { get; }

    /// <inheritdoc />
    public abstract IPublisher Publisher { get; }

    /// <inheritdoc />
    public virtual ITokenRefresher? Refresher => null;

    /// <summary>Guide steps from numbered keys <c>guide.{code}.1..n</c>; links and copy fields by step number.</summary>
    protected SetupGuide Steps(int count, IReadOnlyDictionary<int, Uri>? links = null, IReadOnlyDictionary<int, string>? copy = null, params string[] warnings) =>
        new(Enumerable.Range(1, count).Select(i => new GuideStep($"guide.{Code}.{i}", links?.GetValueOrDefault(i), copy?.GetValueOrDefault(i))).ToList(),
            warnings);
}
