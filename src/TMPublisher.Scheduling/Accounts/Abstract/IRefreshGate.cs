namespace TMPublisher.Scheduling.Accounts;

/// <summary>
/// One token refresh per account at a time, across processes (the window and a run the system scheduler started): with a rotating refresh token
/// (X) a second refresh with the old token is refused and would wrongly mark a working account "connect again".
/// </summary>
public interface IRefreshGate
{
    /// <summary>Waits until no other refresh of the account runs (in this process or the other one); disposing the result lets the next one in.</summary>
    Task<IAsyncDisposable> EnterAsync(Guid accountId, CancellationToken cancellationToken);
}
