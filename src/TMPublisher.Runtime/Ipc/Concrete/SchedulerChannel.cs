using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TMPublisher.Runtime.Notifications;

namespace TMPublisher.Runtime.Ipc;

/// <summary>
/// The local channel between the window and the background scheduler of one data folder: a named pipe on Windows, a Unix domain socket on
/// Linux (.NET maps the pipe name to a socket file in the temp folder). Both ends use <see cref="PipeOptions.CurrentUserOnly"/>: only the same
/// user's processes can connect (the master-password key goes over it). One UTF-8 line per message.
/// </summary>
public static class SchedulerChannel
{
    /// <summary>Longest line read (a notification, a state); a longer one ends the connection.</summary>
    public const int MaxLine = 64 * 1024;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The channel name of a data folder.</summary>
    public static string PipeName(string dataDirectory)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataDirectory).ToUpperInvariant())))[..16];
        return "TMPublisher-" + hash + "-scheduler";
    }

    /// <summary>Options of both ends.</summary>
    public static PipeOptions Options => PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;

    /// <summary>Writes one line.</summary>
    public static async Task WriteLineAsync(Stream stream, string line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(line);
        await stream.WriteAsync(Utf8.GetBytes(line + "\n"), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads one line; <c>null</c> when the other end closed the connection.</summary>
    public static async Task<string?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var buffer = new MemoryStream();
        var one = new byte[1];
        while (true)
        {
            if (await stream.ReadAsync(one, cancellationToken).ConfigureAwait(false) == 0)
            {
                return buffer.Length > 0 ? Utf8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length) : null;
            }

            if (one[0] == (byte)'\n')
            {
                return Utf8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            }

            if (buffer.Length >= MaxLine)
            {
                throw new InvalidDataException("A message on the scheduler channel is longer than allowed.");
            }

            buffer.WriteByte(one[0]);
        }
    }

    /// <summary>A notification event line.</summary>
    public static string NotifyLine(NotificationMessage message) =>
        SchedulerCommands.Notify + " " + JsonSerializer.Serialize(message, RuntimeJsonContext.Default.NotificationMessage);

    /// <summary>The notification of an event line or a notification-process input line; <c>null</c> when it is not one.</summary>
    public static NotificationMessage? ParseNotification(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var json = line.StartsWith(SchedulerCommands.Notify + " ", StringComparison.Ordinal) ? line[(SchedulerCommands.Notify.Length + 1)..] : line;
        return json.StartsWith('{') ? JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.NotificationMessage) : null;
    }

    /// <summary>The JSON of a notification (one input line of the notification process).</summary>
    public static string NotificationJson(NotificationMessage message) => JsonSerializer.Serialize(message, RuntimeJsonContext.Default.NotificationMessage);

    /// <summary>The JSON of a state.</summary>
    public static string StateJson(SchedulerState state) => JsonSerializer.Serialize(state, RuntimeJsonContext.Default.SchedulerState);

    /// <summary>The state of a status answer; <c>null</c> when it is not one.</summary>
    public static SchedulerState? ParseState(string line) =>
        line is { Length: > 0 } && line[0] == '{' ? JsonSerializer.Deserialize(line, RuntimeJsonContext.Default.SchedulerState) : null;
}
