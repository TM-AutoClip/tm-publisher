using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Meta;

namespace TMPublisher.Platforms.Threads;

/// <summary>
/// Threads text post: container (<c>POST /{user}/threads</c> media_type=TEXT), a short wait until the container is FINISHED, then
/// <c>POST /{user}/threads_publish</c>; the address is the post's <c>permalink</c>.
/// </summary>
public sealed class ThreadsPublisher(HttpTransport transport, ILogger<ThreadsPublisher> logger) : PublisherBase(transport, TimeSpan.FromMinutes(5), logger)
{
    /// <inheritdoc />
    public override async Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = request.Credentials.Require(CredentialKeys.UserId);
        var session = Session(request);
        var container = await SendJsonAsync(() => Form(ThreadsApi.Graph + user + "/threads", session,
            [new("media_type", "TEXT"), new("text", request.Target.Text)]), null, "container", cancellationToken).ConfigureAwait(false);
        var id = JsonSafe.Str(container, "id");
        await PollAsync(async (_, ct) =>
        {
            var state = await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Get, ThreadsApi.Graph + id + "?fields=status,error_message&access_token="
                + Uri.EscapeDataString(session.Token)), null, "container status", ct).ConfigureAwait(false);
            return JsonSafe.Str(state, "status") switch
            {
                "FINISHED" or "" => PollStep<bool>.Done(true),
                "ERROR" or "EXPIRED" => throw new PlatformRejectedException("Threads could not prepare the post: " + JsonSafe.Str(state, "error_message")),
                _ => PollStep<bool>.Again(TimeSpan.FromSeconds(3)),
            };
        }, TimeSpan.Zero, TimeSpan.FromMinutes(2), () => new PlatformRejectedException("Threads did not prepare the post in 2 minutes."), cancellationToken).ConfigureAwait(false);

        var published = await SendJsonAsync(() => Form(ThreadsApi.Graph + user + "/threads_publish", session, [new("creation_id", id)]), null, "publish", cancellationToken)
            .ConfigureAwait(false);
        var postId = JsonSafe.Str(published, "id");
        var permalink = string.Empty;
        try
        {
            permalink = JsonSafe.Str(await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Get, ThreadsApi.Graph + postId + "?fields=permalink&access_token="
                + Uri.EscapeDataString(session.Token)), null, "permalink", cancellationToken).ConfigureAwait(false), "permalink");
        }
        catch (PlatformRejectedException ex)
        {
            Logger.LogWarning(ex, "Threads permalink could not be read for {PostId}", postId);
        }

        progress?.Report(1);
        Logger.LogInformation("Threads post {PostId} published", postId);
        return new PublishResult(postId, permalink, TargetStatus.Published);
    }

    /// <inheritdoc />
    protected override Exception Classify(string step, int status, string body) => MetaErrors.Classify("Threads", step, status, body);

    private static HttpRequestMessage Form(string address, TokenSession session, List<KeyValuePair<string, string>> fields)
    {
        fields.Add(new("access_token", session.Token));
        return new HttpRequestMessage(HttpMethod.Post, address) { Content = new FormUrlEncodedContent(fields) };
    }
}
