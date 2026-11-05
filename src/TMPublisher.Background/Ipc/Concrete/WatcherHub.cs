using Microsoft.Extensions.Logging;
using TMPublisher.Runtime.Ipc;

namespace TMPublisher.Background.Ipc;

/// <summary>The open windows' event connections (<see cref="SchedulerCommands.Watch"/>): "changed" and notification lines go to each.</summary>
public sealed class WatcherHub(ILogger<WatcherHub> logger)
{
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(3);
    private readonly List<Stream> _watchers = [];
    private readonly Lock _gate = new();

    /// <summary>Whether a window listens now.</summary>
    public bool HasWatchers
    {
        get
        {
            lock (_gate)
            {
                return _watchers.Count > 0;
            }
        }
    }

    /// <summary>Adds a connection.</summary>
    public void Add(Stream stream)
    {
        lock (_gate)
        {
            _watchers.Add(stream);
        }

        logger.LogInformation("A window listens for events now");
    }

    /// <summary>Removes a connection.</summary>
    public void Remove(Stream stream)
    {
        bool removed;
        lock (_gate)
        {
            removed = _watchers.Remove(stream);
        }

        if (removed)
        {
            logger.LogInformation("A window stopped listening for events");
        }
    }

    /// <summary>Writes the line to every window; returns how many got it (a broken connection is dropped and logged).</summary>
    public async Task<int> BroadcastAsync(string line)
    {
        Stream[] all;
        lock (_gate)
        {
            all = [.. _watchers];
        }

        var delivered = 0;
        foreach (var stream in all)
        {
            try
            {
                using var timeout = new CancellationTokenSource(WriteTimeout);
                await SchedulerChannel.WriteLineAsync(stream, line, timeout.Token).ConfigureAwait(false);
                delivered++;
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            {
                logger.LogWarning("An event could not be written to a window ({Type}: {Reason}); the connection is dropped", ex.GetType().Name, ex.Message);
                Remove(stream);
            }
        }

        return delivered;
    }
}
