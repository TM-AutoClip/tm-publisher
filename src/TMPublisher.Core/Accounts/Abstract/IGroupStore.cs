namespace TMPublisher.Core.Accounts;

/// <summary>Account groups.</summary>
public interface IGroupStore
{
    /// <summary>All groups.</summary>
    Task<IReadOnlyList<Group>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Inserts or updates.</summary>
    Task SaveAsync(Group group, CancellationToken cancellationToken);

    /// <summary>Deletes.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
