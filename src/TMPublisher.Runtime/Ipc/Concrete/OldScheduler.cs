using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace TMPublisher.Runtime.Ipc;

/// <summary>
/// The always-running background scheduler of 0.1.3–0.1.4 (<c>TMPublisherScheduler</c>), known only to end it after an update: it listened on a
/// named pipe (a Unix domain socket on Linux) of the data folder, same user only, one UTF-8 line per request. Only its <c>quit</c> request is
/// used ("ok" = it ends now, "busy" = a post is being sent). Nothing else of that protocol is left.
/// </summary>
public static class OldScheduler
{
    private const string Quit = "quit";
    private const string Ok = "ok";
    private const string Busy = "busy";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(10);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The pipe name 0.1.3–0.1.4 used for <paramref name="dataDirectory"/>.</summary>
    public static string PipeName(string dataDirectory)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataDirectory).ToUpperInvariant())))[..16];
        return "TMPublisher-" + hash + "-scheduler";
    }

    /// <summary>
    /// Asks a running 0.1.3–0.1.4 scheduler of the data folder to end. <see cref="OldSchedulerAnswer.NotRunning"/> when nothing listens (then the
    /// lock is held by this version's window or run).
    /// </summary>
    public static async Task<OldSchedulerAnswer> AskToQuitAsync(string dataDirectory, CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(".", PipeName(dataDirectory), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await using (pipe.ConfigureAwait(false))
        {
            try
            {
                using (var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    connect.CancelAfter(ConnectTimeout);
                    await pipe.ConnectAsync(connect.Token).ConfigureAwait(false);
                }

                using var answer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                answer.CancelAfter(AnswerTimeout);
                await pipe.WriteAsync(Utf8.GetBytes(Quit + "\n"), answer.Token).ConfigureAwait(false);
                await pipe.FlushAsync(answer.Token).ConfigureAwait(false);
                return await ReadLineAsync(pipe, answer.Token).ConfigureAwait(false) switch
                {
                    Ok => OldSchedulerAnswer.Quitting,
                    Busy => OldSchedulerAnswer.Busy,
                    _ => OldSchedulerAnswer.Unexpected,
                };
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException
                || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                return OldSchedulerAnswer.NotRunning;
            }
        }
    }

    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new List<byte>();
        var one = new byte[1];
        while (buffer.Count < 1024 && await stream.ReadAsync(one, cancellationToken).ConfigureAwait(false) == 1)
        {
            if (one[0] == (byte)'\n')
            {
                break;
            }

            buffer.Add(one[0]);
        }

        return buffer.Count == 0 ? null : Utf8.GetString([.. buffer]);
    }
}
