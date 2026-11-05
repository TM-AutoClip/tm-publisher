namespace TMPublisher.Core.Settings;

/// <summary>Per-platform default tag sets.</summary>
public interface ITagSetStore
{
    /// <summary>All stored sets (platforms without a stored set use <see cref="StarterTagSets"/>).</summary>
    Task<IReadOnlyList<TagSet>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Stores one platform's set.</summary>
    Task SaveAsync(TagSet set, CancellationToken cancellationToken);
}
