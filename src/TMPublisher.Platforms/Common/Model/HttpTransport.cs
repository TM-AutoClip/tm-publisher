using Microsoft.Extensions.Logging;

namespace TMPublisher.Platforms.Common;

/// <summary>
/// Where platform HTTP goes (tests give a fake handler; no test reaches the network). Redirects are OFF: YouTube's resumable upload answers
/// 308 for "chunk received"; .NET would follow it as a redirect and send the chunk twice. With a <see cref="Logger"/> every request leaves one
/// line in the log (<see cref="HttpLogHandler"/>).
/// </summary>
public sealed record HttpTransport(Func<HttpMessageHandler> CreateHandler)
{
    /// <summary>The real network.</summary>
    public static HttpTransport Network { get; } = new(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    });

    /// <summary>Where each request's outcome is logged; <c>null</c> logs nothing.</summary>
    public ILogger? Logger { get; init; }

    /// <summary>The same transport, logging each request to <paramref name="logger"/>.</summary>
    public HttpTransport WithLog(ILogger logger) => this with { Logger = logger };

    /// <summary>A client with the given timeout.</summary>
    public HttpClient CreateClient(TimeSpan timeout)
    {
        var handler = CreateHandler();
        var client = new HttpClient(Logger is null ? handler : new HttpLogHandler(handler, Logger, timeout)) { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TMPublisher/" + (typeof(HttpTransport).Assembly.GetName().Version?.ToString(3) ?? "0"));
        return client;
    }
}
