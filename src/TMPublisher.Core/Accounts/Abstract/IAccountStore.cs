namespace TMPublisher.Core.Accounts;

/// <summary>Connected accounts (without secrets).</summary>
public interface IAccountStore
{
    /// <summary>All accounts.</summary>
    Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken);

    /// <summary>One account; <c>null</c> when missing.</summary>
    Task<Account?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Inserts or updates.</summary>
    Task SaveAsync(Account account, CancellationToken cancellationToken);

    /// <summary>Deletes the account, its secrets and its group memberships.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
