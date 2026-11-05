using System.IO.Pipes;

namespace TMPublisher.Runtime.Ipc;

/// <summary>The window's end of <see cref="SchedulerChannel"/>: one short connection per request.</summary>
public sealed class SchedulerClient(string dataDirectory)
{
    /// <summary>How long a request waits for the scheduler to accept the connection.</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(1);

    /// <summary>How long a request waits for the answer (a key check opens the database).</summary>
    public static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The channel name.</summary>
    public string PipeName { get; } = SchedulerChannel.PipeName(dataDirectory);

    /// <summary>Sends one request line; the answer, or why the scheduler was not reached (it does not run, it did not answer in time).</summary>
    public async Task<SchedulerReply> SendAsync(string command, string? argument, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(command);
        try
        {
            await using var pipe = await ConnectAsync(cancellationToken).ConfigureAwait(false);
            using var answer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            answer.CancelAfter(AnswerTimeout);
            await SchedulerChannel.WriteLineAsync(pipe, argument is null ? command : command + " " + argument, answer.Token).ConfigureAwait(false);
            var line = await SchedulerChannel.ReadLineAsync(pipe, answer.Token).ConfigureAwait(false);
            return line is null ? new SchedulerReply(false, "The scheduler closed the connection without an answer.") : new SchedulerReply(true, line);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or InvalidDataException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new SchedulerReply(false, ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>The scheduler's state; <see cref="SchedulerStatus.NotRunning"/> (with the reason in the message) when it does not answer.</summary>
    public async Task<SchedulerState> StatusAsync(CancellationToken cancellationToken)
    {
        var reply = await SendAsync(SchedulerCommands.Status, null, cancellationToken).ConfigureAwait(false);
        return reply.Reached && SchedulerChannel.ParseState(reply.Text) is { } state
            ? state
            : new SchedulerState { Status = SchedulerStatus.NotRunning, Message = reply.Text };
    }

    /// <summary>
    /// Keeps a connection open and passes every event line to <paramref name="onEvent"/> until the scheduler closes it or
    /// <paramref name="cancellationToken"/> ends. Returns the reason the connection ended.
    /// </summary>
    public async Task<string> WatchAsync(Action<string> onEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onEvent);
        try
        {
            await using var pipe = await ConnectAsync(cancellationToken).ConfigureAwait(false);
            await SchedulerChannel.WriteLineAsync(pipe, SchedulerCommands.Watch, cancellationToken).ConfigureAwait(false);
            if (await SchedulerChannel.ReadLineAsync(pipe, cancellationToken).ConfigureAwait(false) != SchedulerCommands.Ok)
            {
                return "The scheduler refused to send events.";
            }

            while (await SchedulerChannel.ReadLineAsync(pipe, cancellationToken).ConfigureAwait(false) is { } line)
            {
                onEvent(line);
            }

            return "The scheduler closed the connection.";
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or InvalidDataException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    private async Task<NamedPipeClientStream> ConnectAsync(CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, SchedulerChannel.Options);
        try
        {
            using var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connect.CancelAfter(ConnectTimeout);
            await pipe.ConnectAsync(connect.Token).ConfigureAwait(false);
            return pipe;
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
