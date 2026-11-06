using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Platforms.Common;

/// <summary>
/// One log line per platform request: method, host, path, HTTP status and duration (Information when it worked, Warning when the platform
/// refused it or it did not arrive; whoever made the request logs what that means for the post). Never the query, the headers or a body: they
/// carry tokens and the user's text. Path parts that look like an id or a token are written as <c>*</c>, and the server of a Mastodon or
/// Bluesky account as <see cref="PlatformHosts.Other"/>.
/// </summary>
public sealed partial class HttpLogHandler(HttpMessageHandler inner, ILogger logger) : DelegatingHandler(inner)
{
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
            logger.LogInformation("HTTP {Method} {Host}{Path}: canceled after {Elapsed} ms", request.Method.Method, host, path,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
        {
            // No answer (network, time-out): the caller retries or reports it; the type and reason are kept here.
            logger.LogWarning("HTTP {Method} {Host}{Path}: no answer after {Elapsed} ms ({Type}: {Reason})", request.Method.Method, host, path,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    /// <summary>The host as it may be logged.</summary>
    internal static string Host(Uri? address)
    {
        if (address is null || !address.IsAbsoluteUri)
        {
            return PlatformHosts.Other;
        }

        var host = address.Host.ToLowerInvariant();
        return PlatformHosts.Known.Any(k => host == k || host.EndsWith("." + k, StringComparison.Ordinal)) ? host : PlatformHosts.Other;
    }

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
