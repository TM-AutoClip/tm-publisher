using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Localization;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Quotas;
using TMPublisher.Core.Security;
using TMPublisher.Core.Validation;
using TMPublisher.Scheduling.Accounts;

namespace TMPublisher.Scheduling;

/// <summary>
/// Sends one post that the scheduler has claimed (<see cref="PostScheduler"/>): a check without network first (account connected, files and text within the rules, local quota), and when anything
/// fails nothing is sent and the post is "blocked". Then the targets go one by one in the user's order; when one fails the rest wait and
/// the post is "stopped" (retry / skip / cancel). Every step is stored at once and logged; tokens never reach the log.
/// </summary>
public sealed class PostSender(IPostStore posts, IAccountStore accounts, ICredentialStore credentials, IPlatformDirectory platforms, CredentialRenewer renewer,
    QuotaChecker quotas, IPublishLog log, INotifier notifier, ILocalizer text, TimeProvider clock, ILogger<PostSender> logger)
{
    /// <summary>Overridable file check (tests).</summary>
    internal Func<string, bool>? FileExists { get; set; }

    /// <summary>Raised when a target is "processing" after upload (the processing watcher follows it).</summary>
    public event EventHandler<(Post Post, PostTarget Target)>? Processing;

    /// <summary>Sends the pending targets of the post; returns the post's state afterwards.</summary>
    public async Task<PostStatus> SendAsync(Post post, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(post);
        if (post.Status != PostStatus.Publishing)
        {
            // The only way to send is the claim (scheduled -> publishing in the database), so nothing is ever sent twice.
            throw new InvalidOperationException($"Post {post.Id} is {post.Status}; it must be claimed (publishing) before it is sent.");
        }

        var pending = post.Targets.Where(t => t.Status == TargetStatus.Pending).OrderBy(t => t.Order).ToList();
        if (pending.Count == 0 && post.Targets.Count == 0)
        {
            return await BlockAsync(post, [], text["problem.no_targets"], cancellationToken).ConfigureAwait(false);
        }

        var resolved = new List<(PostTarget Target, Account Account, IPlatform Platform)>();
        var problems = new List<(PostTarget Target, string Message)>();
        foreach (var target in pending)
        {
            var (account, platform, problem) = await PreflightAsync(post, target, cancellationToken).ConfigureAwait(false);
            if (problem is not null)
            {
                problems.Add((target, problem));
            }
            else
            {
                resolved.Add((target, account!, platform!));
            }
        }

        if (problems.Count > 0)
        {
            return await BlockAsync(post, problems, problems[0].Message, cancellationToken).ConfigureAwait(false);
        }

        post.Status = PostStatus.Publishing;
        post.UpdatedAtUtc = clock.GetUtcNow();
        await posts.SaveAsync(post, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Post {PostId}: sending to {Count} targets", post.Id, resolved.Count);
        foreach (var (target, account, platform) in resolved)
        {
            if (!await SendTargetAsync(post, target, account, platform, cancellationToken).ConfigureAwait(false))
            {
                post.Status = PostStatus.Stopped;
                post.UpdatedAtUtc = clock.GetUtcNow();
                await posts.SaveAsync(post, cancellationToken).ConfigureAwait(false);
                return post.Status;
            }
        }

        post.Status = PostStatus.Done;
        post.UpdatedAtUtc = clock.GetUtcNow();
        await posts.SaveAsync(post, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Post {PostId}: done", post.Id);
        return post.Status;
    }

    private async Task<(Account? Account, IPlatform? Platform, string? Problem)> PreflightAsync(Post post, PostTarget target, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(target.AccountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return (null, null, text["problem.account_removed"]);
        }

        if (platforms.Find(account.Platform) is not { } platform)
        {
            return (account, null, text.Format("problem.platform_unknown", account.Platform));
        }

        var now = clock.GetUtcNow();
        var secrets = await credentials.GetAsync(account.Id, cancellationToken).ConfigureAwait(false);
        var expired = secrets.AccessExpiresAtUtc is { } expiry && expiry <= now && platform.Refresher is null;
        if (account.State == AccountState.NeedsReconnect || account.ReconnectByUtc <= now || expired || secrets.Values.Count == 0)
        {
            return (account, platform, text.Format("problem.reconnect", platform.DisplayName, account.DisplayName));
        }

        if (PostValidator.Validate(platform, post, target, FileExists) is { Count: > 0 } invalid)
        {
            return (account, platform, platform.DisplayName + " · " + account.DisplayName + ": " + string.Join("; ", invalid.Select(p => text.Format(p.Key, p.Args))));
        }

        if (await quotas.FullRuleAsync(account, platform, cancellationToken).ConfigureAwait(false) is { } rule)
        {
            return (account, platform, text.Format("problem.quota", platform.DisplayName, account.DisplayName, rule.MaxPosts, text["quota." + rule.Window.ToString().ToLowerInvariant()]));
        }

        return (account, platform, null);
    }

    private async Task<PostStatus> BlockAsync(Post post, List<(PostTarget Target, string Message)> problems, string summary, CancellationToken cancellationToken)
    {
        foreach (var (target, message) in problems)
        {
            target.Message = message;
        }

        post.Status = PostStatus.Blocked;
        post.UpdatedAtUtc = clock.GetUtcNow();
        await posts.SaveAsync(post, cancellationToken).ConfigureAwait(false);
        logger.LogWarning("Post {PostId} blocked before sending: {Reason}", post.Id, Redactor.Redact(summary));
        notifier.Notify(new AppNotification(NotificationKind.Failed, text["notify.blocked"], summary));
        return post.Status;
    }

    private async Task<bool> SendTargetAsync(Post post, PostTarget target, Account account, IPlatform platform, CancellationToken cancellationToken)
    {
        var who = platform.DisplayName + " · " + account.DisplayName;
        target.Status = TargetStatus.Publishing;
        target.Message = null;
        await posts.SaveTargetAsync(target, cancellationToken).ConfigureAwait(false);
        try
        {
            var fresh = await renewer.GetFreshAsync(account, cancellationToken).ConfigureAwait(false);
            var result = await platform.Publisher.PublishAsync(new PublishRequest(account, fresh, post, target, renewer.RenewFor(account, fresh)), null, cancellationToken)
                .ConfigureAwait(false);
            var now = clock.GetUtcNow();
            target.Status = result.State;
            target.ExternalId = result.ExternalId;
            target.Url = result.Url.Length > 0 ? result.Url : null;
            target.Message = result.NoteKey is { } note ? text[note] : null;
            target.PublishedAtUtc = now;
            await posts.SaveTargetAsync(target, cancellationToken).ConfigureAwait(false);
            await log.AddAsync(new PublishLogEntry(account.Id, platform.Code, post.Id, now, Success: true), cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Post {PostId}: {Platform} target {TargetId} is {State}", post.Id, platform.Code, target.Id, result.State);
            switch (result.State)
            {
                case TargetStatus.NeedsApproval:
                    notifier.Notify(new AppNotification(NotificationKind.NeedsApproval, text["notify.approval"], who + ": " + target.Message));
                    break;
                case TargetStatus.Processing:
                    Processing?.Invoke(this, (post, target));
                    break;
                default:
                    notifier.Notify(new AppNotification(NotificationKind.Published, text["notify.published"], who));
                    break;
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var reason = Redactor.Redact(ex.Message);
            target.Status = TargetStatus.Failed;
            target.Message = ex is PlatformAuthException ? text.Format("problem.auth_failed", reason) : reason;
            await posts.SaveTargetAsync(target, CancellationToken.None).ConfigureAwait(false);
            await log.AddAsync(new PublishLogEntry(account.Id, platform.Code, post.Id, clock.GetUtcNow(), Success: false), CancellationToken.None).ConfigureAwait(false);
            if (ex is PlatformAuthException)
            {
                await renewer.MarkReconnectAsync(account, CancellationToken.None).ConfigureAwait(false);
            }

            logger.LogError("Post {PostId}: {Platform} target {TargetId} failed ({Type}): {Reason}", post.Id, platform.Code, target.Id, ex.GetType().Name, reason);
            notifier.Notify(new AppNotification(NotificationKind.Failed, text["notify.failed"], who + ": " + target.Message));
            return false;
        }
    }
}
