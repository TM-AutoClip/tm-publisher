using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Security;

namespace TMPublisher.Platforms.Common;

/// <summary>
/// One log line per platform request: method, host, path, HTTP status and duration (Information when it worked, Warning when the platform
/// refused it or it did not arrive; whoever made the request logs what that means for the post). Never the query, the headers or a body: they
/// carry tokens and the user's text. Path parts that look like an id or a token are written as <c>*</c>, and the server of a Mastodon or
/// Bluesky account as <see cref="PlatformHosts.Other"/> (also inside an exception message: the name is registered with
/// <see cref="AccountHosts"/>). <paramref name="timeout"/> is the client's time-out: HttpClient enforces it by cancelling the token it hands
/// down here, so a cancel that comes when the time is up is a request without an answer (Warning), not the user's Stop (Information).
/// </summary>
public sealed partial class HttpLogHandler(HttpMessageHandler inner, ILogger logger, TimeSpan? timeout = null) : DelegatingHandler(inner)
{
    // HttpClient starts its time-out clock just before it calls this handler, and timers are coarse: a cancel this close to the time-out (at
    // most a tenth of it) is the time-out.
    private static readonly TimeSpan TimeoutSlack = TimeSpan.FromMilliseconds(250);

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = Stopwatch.GetTimestamp();
        var host = Host(request.RequestUri);
        var path = Path(request.RequestUri);
        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (status < 400)
            {
                logger.LogInformation("HTTP {Method} {Host}{Path}: {Status} in {Elapsed} ms", request.Method.Method, host, path, status, elapsed);
            }
            else
            {
                logger.LogWarning("HTTP {Method} {Host}{Path}: {Status} in {Elapsed} ms", request.Method.Method, host, path, status, elapsed);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            if (TimedOut(elapsed))
            {
                logger.LogWarning("HTTP {Method} {Host}{Path}: no answer within the time-out of {Timeout} ms (after {Elapsed} ms)", request.Method.Method,
                    host, path, (long)timeout!.Value.TotalMilliseconds, (long)elapsed.TotalMilliseconds);
            }
            else
            {
                logger.LogInformation("HTTP {Method} {Host}{Path}: canceled after {Elapsed} ms", request.Method.Method, host, path,
                    (long)elapsed.TotalMilliseconds);
            }

            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
        {
            // No answer (network, time-out): the caller retries or reports it; the type and reason are kept here.
            logger.LogWarning("HTTP {Method} {Host}{Path}: no answer after {Elapsed} ms ({Type}: {Reason})", request.Method.Method, host, path,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, ex.GetType().Name, AccountHosts.Mask(ex.Message));
            throw;
        }
    }

    /// <summary>The host as it may be logged (<see cref="PlatformHosts.Display"/>).</summary>
    internal static string Host(Uri? address) => PlatformHosts.Display(address);

    private bool TimedOut(TimeSpan elapsed) =>
        timeout is { } limit && limit > TimeSpan.Zero && limit != Timeout.InfiniteTimeSpan
        && elapsed >= limit - TimeSpan.FromTicks(Math.Min(TimeoutSlack.Ticks, limit.Ticks / 10));

    /// <summary>The path as it may be logged: every part that is not a plain word (or an API version such as v3, v21.0 or 2) becomes <c>*</c>; no query.</summary>
    internal static string Path(Uri? address)
    {
        if (address is null || !address.IsAbsoluteUri)
        {
            return "/";
        }

        var text = new StringBuilder();
        foreach (var part in address.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            text.Append('/').Append(Word().IsMatch(part) || Version().IsMatch(part) ? part : "*");
        }

        return text.Length == 0 ? "/" : text.ToString();
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z_.-]{0,39}$")]
    private static partial Regex Word();

    [GeneratedRegex(@"^v?\d{1,2}(?:\.\d{1,2})?$")]
    private static partial Regex Version();
}
