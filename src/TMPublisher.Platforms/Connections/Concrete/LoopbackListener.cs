using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Platforms.Connections;

/// <summary>
/// <see cref="ILoopbackListener"/>: a TCP listener on 127.0.0.1 only (not HttpListener: it needs no administrator rights or URL
/// reservation). The first valid <c>GET /callback</c> is taken; other paths (the browser's icon request) get 404.
/// </summary>
public sealed class LoopbackListener(ILogger<LoopbackListener> logger) : ILoopbackListener
{
    /// <summary>Path of the return address.</summary>
    public const string CallbackPath = "/callback";

    /// <inheritdoc />
    public ILoopbackSession Start(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        return new Session(listener, logger);
    }

    /// <summary>One session.</summary>
    private sealed class Session(TcpListener listener, ILogger logger) : ILoopbackSession
    {
        private const string Done = "<!doctype html><meta charset=\"utf-8\"><title>TM Publisher</title>"
            + "<body style=\"font-family:sans-serif;background:#121C30;color:#E8EEF7;padding:40px\">"
            + "<h2>TM Publisher</h2><p>You can close this tab and go back to TM Publisher.</p>"
            + "<p>Bu sekmeyi kapatıp TM Publisher'a dönebilirsiniz.</p></body>";

        public async Task<LoopbackCallback> WaitAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                string? target;
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    target = await ReadTargetAsync(client.GetStream(), timeout.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug(ex, "Loopback request could not be read");
                    continue;
                }

                var parts = target?.Split('?', 2);
                if (parts is null || parts[0] != CallbackPath)
                {
                    await RespondAsync(client, "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n").ConfigureAwait(false);
                    continue;
                }

                var query = Parse(parts.Length > 1 ? parts[1] : string.Empty);
                var body = Encoding.UTF8.GetBytes(Done);
                await RespondAsync(client, "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nContent-Length: "
                    + body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\nConnection: close\r\n\r\n" + Done).ConfigureAwait(false);
                return new LoopbackCallback(query.GetValueOrDefault("code"), query.GetValueOrDefault("state"),
                    query.GetValueOrDefault("error_description") ?? query.GetValueOrDefault("error"));
            }
        }

        public void Dispose()
        {
            listener.Stop();
            listener.Dispose();
        }

        private static async Task<string?> ReadTargetAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[16 * 1024];
            var read = 0;
            while (read < buffer.Length)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                read += count;
                if (buffer.AsSpan(0, read).IndexOf("\r\n\r\n"u8) >= 0)
                {
                    break;
                }
            }

            var line = Encoding.ASCII.GetString(buffer, 0, read).Split("\r\n", 2)[0].Split(' ');
            return line.Length >= 2 && line[0] == "GET" ? line[1] : null;
        }

        private static async Task RespondAsync(TcpClient client, string response)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(response);
                await client.GetStream().WriteAsync(bytes).ConfigureAwait(false);
                await client.GetStream().FlushAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
                // The browser closed the connection; the answer is not needed.
            }
        }
    }

    /// <summary>Query string values.</summary>
    internal static Dictionary<string, string> Parse(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            values.TryAdd(Uri.UnescapeDataString(pair[0].Replace('+', ' ')), pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty);
        }

        return values;
    }
}
