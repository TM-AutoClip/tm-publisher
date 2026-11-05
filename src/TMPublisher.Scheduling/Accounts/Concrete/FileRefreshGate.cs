using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Paths;

namespace TMPublisher.Scheduling.Accounts;

/// <summary>
/// <see cref="IRefreshGate"/> with a semaphore per account in this process and a lock file per account in the data folder
/// (<c>locks/refresh-&lt;account id&gt;.lock</c>, opened without sharing: Windows refuses a second open, .NET takes an exclusive flock on Linux).
/// The lock file is released when the process ends, even when it is killed.
/// </summary>
public sealed class FileRefreshGate(IAppPaths paths, TimeProvider clock, ILogger<FileRefreshGate> logger) : IRefreshGate
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

    /// <inheritdoc />
    public async Task<IAsyncDisposable> EnterAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var gate = _gates.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var folder = Path.Combine(paths.DataDirectory, "locks");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "refresh-" + accountId.ToString("N") + ".lock");
            var started = clock.GetUtcNow();
            var logged = false;
            while (true)
            {
                try
                {
                    return new Held(new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None), gate);
                }
                catch (IOException ex) when (ex is not DirectoryNotFoundException and not PathTooLongException)
                {
                    // The other process refreshes this account now: wait for it (it stores the new tokens, which are read again after this).
                    if (clock.GetUtcNow() - started > SchedulerLimits.RefreshLockWait)
                    {
                        logger.LogError("The token refresh lock of account {AccountId} stayed taken for {Wait}; giving up", accountId, SchedulerLimits.RefreshLockWait);
                        throw new TimeoutException("Another TM Publisher process kept refreshing this account's tokens for too long.", ex);
                    }

                    if (!logged)
                    {
                        logger.LogInformation("Waiting for the other process's token refresh of account {AccountId}", accountId);
                        logged = true;
                    }

                    await Task.Delay(SchedulerLimits.RefreshLockRetry, clock, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            gate.Release();
            throw;
        }
    }

    private sealed class Held(FileStream file, SemaphoreSlim gate) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await file.DisposeAsync().ConfigureAwait(false);
            gate.Release();
        }
    }
}
