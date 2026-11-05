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

    /// <summary>Sets only the state (the rest of the account, which the window may be changing, stays as stored).</summary>
    Task SetStateAsync(Guid id, AccountState state, CancellationToken cancellationToken);

    /// <summary>Deletes the account, its secrets and its group memberships.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
