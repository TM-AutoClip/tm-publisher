using System.IO.Pipes;
using Microsoft.Extensions.Logging;
using TMPublisher.Background.Hosting;
using TMPublisher.Runtime.Ipc;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Background.Ipc;

/// <summary>
/// The scheduler's end of <see cref="SchedulerChannel"/>: accepts connections of the same user only, reads one request line and answers it
/// (<see cref="SchedulerHost.HandleAsync"/>); a <see cref="SchedulerCommands.Watch"/> connection stays open for events. Only command names are
/// logged, never their arguments (the key).
/// </summary>
public sealed class SchedulerServer(AppPaths paths, SchedulerHost host, WatcherHub watchers, ILogger<SchedulerServer> logger) : IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private readonly CancellationTokenSource _stop = new();

    /// <summary>Starts accepting connections.</summary>
    public void Start() => _ = AcceptAsync(SchedulerChannel.PipeName(paths.DataDirectory), _stop.Token);

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task AcceptAsync(string name, CancellationToken cancellationToken)
    {
        logger.LogInformation("Listening for the window on the local channel");
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
                    SchedulerChannel.Options);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                var connection = server;
                server = null;
                _ = ServeAsync(connection, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogError(ex, "The local channel failed; listening again in a second");
                await Task.Delay(1000, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                if (server is not null)
                {
                    await server.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private async Task ServeAsync(NamedPipeServerStream connection, CancellationToken cancellationToken)
    {
        await using var _ = connection.ConfigureAwait(false);
        try
        {
            string? line;
            using (var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                request.CancelAfter(RequestTimeout);
                line = await SchedulerChannel.ReadLineAsync(connection, request.Token).ConfigureAwait(false);
            }

            if (line is null)
            {
                return;
            }

            var space = line.IndexOf(' ', StringComparison.Ordinal);
            var command = space < 0 ? line : line[..space];
            var argument = space < 0 ? null : line[(space + 1)..];
            if (command == SchedulerCommands.Watch)
            {
                await SchedulerChannel.WriteLineAsync(connection, SchedulerCommands.Ok, cancellationToken).ConfigureAwait(false);
                watchers.Add(connection);
                try
                {
                    // The window never writes on this connection; the read ends when the window closes it.
                    while (await SchedulerChannel.ReadLineAsync(connection, cancellationToken).ConfigureAwait(false) is not null)
                    {
                    }
                }
                finally
                {
                    watchers.Remove(connection);
                }

                return;
            }

            if (command != SchedulerCommands.Status)
            {
                logger.LogInformation("Request from the window: {Command}", command.Length > 20 ? command[..20] : command);
            }

            var answer = await host.HandleAsync(command, argument).ConfigureAwait(false);
            await SchedulerChannel.WriteLineAsync(connection, answer, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The scheduler is ending.
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidDataException or ObjectDisposedException)
        {
            logger.LogWarning("A request on the local channel failed ({Type}: {Reason})", ex.GetType().Name, ex.Message);
        }
    }
}
