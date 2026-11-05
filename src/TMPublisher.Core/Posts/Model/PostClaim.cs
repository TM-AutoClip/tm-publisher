namespace TMPublisher.Core.Posts;

/// <summary>A conditional state change of a stored post (see <see cref="IPostStore.TryMoveAsync"/>).</summary>
/// <param name="PostId">The post.</param>
/// <param name="From">The state it must still be in.</param>
/// <param name="ScheduledAtUtc">The scheduled time it must still have (<c>null</c> for none).</param>
/// <param name="To">The new state.</param>
public sealed record PostClaim(Guid PostId, PostStatus From, DateTimeOffset? ScheduledAtUtc, PostStatus To);
