using TMPublisher.Core.Posts;

namespace TMPublisher.Core.Publishing;

/// <summary>
/// One look at a processing item: <paramref name="State"/> is <see cref="TargetStatus.Processing"/> while it is still running, otherwise
/// the final state; <paramref name="Url"/> fills in a late address; <paramref name="Message"/> explains a failure.
/// </summary>
public sealed record ProcessingCheck(TargetStatus State, string? Url = null, string? Message = null);
