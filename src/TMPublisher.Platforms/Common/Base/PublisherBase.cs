using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Validation;

namespace TMPublisher.Platforms.Common;

/// <summary>
/// Shared body of the publishers (ported from TM AutoClip's uploader base): file check before any request, "on 401 renew the token once and
/// send the same request again", "failed answer → the platform's exception", "poll until processed" (limit + interval), short error text.
/// Platform-specific parts stay in the platform class: <see cref="Classify"/> (which exception an answer is) and the protocol itself.
/// </summary>
public abstract class PublisherBase : IPublisher
{
    /// <summary>Creates with a client of the given timeout.</summary>
    protected PublisherBase(HttpTransport transport, TimeSpan timeout, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(transport);
        Transport = transport;
        Http = new PlatformHttp(transport.CreateClient(timeout));
        Logger = logger;
    }

    /// <summary>Transport (for extra clients).</summary>
    protected HttpTransport Transport { get; }

    /// <summary>HTTP.</summary>
    protected PlatformHttp Http { get; }

    /// <summary>Log.</summary>
    protected ILogger Logger { get; }

    /// <summary>Waiting (instant in tests).</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

    /// <inheritdoc />
    public abstract Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken);

    /// <inheritdoc />
    public virtual Task<ProcessingCheck> CheckProcessingAsync(PublishRequest request, string externalId, CancellationToken cancellationToken) =>
        Task.FromResult(new ProcessingCheck(TargetStatus.Published));

    /// <summary>Turns a failed answer into the platform's exception.</summary>
    protected abstract Exception Classify(string step, int status, string body);

    /// <summary>Whether the answer rejects the token (renew and resend); default HTTP 401.</summary>
    protected virtual bool IsTokenRejected(PlatformReply reply) => reply.Status == 401;

    /// <summary>The token session of a request (the access token and the renew function).</summary>
    protected static TokenSession Session(PublishRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var renew = request.Renew;
        return new TokenSession(request.Credentials.Require(CredentialKeys.AccessToken),
            renew is null ? null : async ct => (await renew(ct).ConfigureAwait(false)).Require(CredentialKeys.AccessToken));
    }

    /// <summary>
    /// Checks a file without any request: exists, not empty, not larger than <paramref name="maxBytes"/> (0 = no limit). The validator
    /// blocks the same cases earlier; the publisher still stops before sending anything.
    /// </summary>
    protected static FileInfo CheckFile(MediaItem item, long maxBytes, string platformName)
    {
        ArgumentNullException.ThrowIfNull(item);
        var file = new FileInfo(item.Path);
        if (!file.Exists)
        {
            throw new PlatformRejectedException($"File not found: {file.Name}");
        }

        if (file.Length == 0)
        {
            throw new PlatformRejectedException($"File is empty: {file.Name}");
        }

        if (maxBytes > 0 && file.Length > maxBytes)
        {
            throw new PlatformRejectedException($"{platformName} accepts files up to {PostValidator.SizeText(maxBytes)}; {file.Name} is {PostValidator.SizeText(file.Length)}.");
        }

        return file;
    }

    /// <summary>
    /// Sends and reads. With a <paramref name="session"/> the request carries the bearer token and a rejected token is renewed once and
    /// the request rebuilt. <paramref name="beforeSend"/> runs before every send (rate limit).
    /// </summary>
    protected async Task<PlatformReply> SendAsync(Func<HttpRequestMessage> build, TokenSession? session, Func<CancellationToken, Task>? beforeSend,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        while (true)
        {
            if (beforeSend is not null)
            {
                await beforeSend(cancellationToken).ConfigureAwait(false);
            }

            using var request = build();
            if (session is not null)
            {
                Authorize(request, session.Token);
            }

            var reply = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (session is not null && IsTokenRejected(reply) && await session.RenewAsync(cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            return reply;
        }
    }

    /// <summary><see cref="SendAsync"/>; a failed answer becomes <see cref="Classify"/>'s exception; returns the JSON body.</summary>
    protected async Task<JsonNode?> SendJsonAsync(Func<HttpRequestMessage> build, TokenSession? session, string step, CancellationToken cancellationToken)
    {
        var reply = await SendAsync(build, session, null, cancellationToken).ConfigureAwait(false);
        return reply.IsSuccess ? JsonSafe.TryParse(reply.Body) : throw Classify(step, reply.Status, reply.Body);
    }

    /// <summary>
    /// "Poll until processed": <paramref name="check"/> asks every round; when not done, waits what it returned. When the total wait reaches
    /// <paramref name="limit"/> the <paramref name="timedOut"/> exception is thrown.
    /// </summary>
    protected async Task<T> PollAsync<T>(Func<int, CancellationToken, Task<PollStep<T>>> check, TimeSpan firstWait, TimeSpan limit, Func<Exception> timedOut,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(timedOut);
        var waited = TimeSpan.Zero;
        if (firstWait > TimeSpan.Zero)
        {
            await Delay(firstWait, cancellationToken).ConfigureAwait(false);
            waited += firstWait;
        }

        for (var round = 0; ; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = await check(round, cancellationToken).ConfigureAwait(false);
            if (step.IsDone)
            {
                return step.Value!;
            }

            if (waited >= limit)
            {
                throw timedOut();
            }

            await Delay(step.Wait, cancellationToken).ConfigureAwait(false);
            waited += step.Wait;
        }
    }

    /// <summary>Writes the bearer token.</summary>
    protected static void Authorize(HttpRequestMessage request, string token)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>A file part for a multipart form (opened per send, closed with the request).</summary>
    protected static StreamContent FilePart(string path)
    {
        var part = new StreamContent(File.OpenRead(path));
        part.Headers.ContentType = new MediaTypeHeaderValue(MediaTypes.Of(path));
        return part;
    }

    /// <summary>The <c>true</c>/<c>false</c> option of a target.</summary>
    protected static bool Flag(PostTarget target, string key) =>
        target is not null && target.Options.TryGetValue(key, out var value) && bool.TryParse(value, out var flag) && flag;
}
