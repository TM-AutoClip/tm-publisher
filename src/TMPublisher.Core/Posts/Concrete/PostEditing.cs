namespace TMPublisher.Core.Posts;

/// <summary>
/// Rules for changing a stored post (compose "edit", calendar move): only drafts and scheduled posts change, and a target that already
/// reached its platform, or that the user skipped or canceled, stays exactly as it was stored. A post that was partly sent and scheduled
/// again would otherwise post twice.
/// </summary>
public static class PostEditing
{
    /// <summary>States in which the user may change a post.</summary>
    public static IReadOnlyCollection<PostStatus> EditableStates { get; } = [PostStatus.Draft, PostStatus.Scheduled];

    /// <summary>The target reached the platform (published, still processing or waiting for approval): it is never sent again.</summary>
    public static bool IsSent(PostTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Status is TargetStatus.Published or TargetStatus.Processing or TargetStatus.NeedsApproval;
    }

    /// <summary>Sent, or skipped / canceled by the user: an edit does not change it.</summary>
    public static bool IsFinished(PostTarget target) =>
        IsSent(target) || target.Status is TargetStatus.Skipped or TargetStatus.Canceled;

    /// <summary>
    /// The targets to store for an edit: an edited target whose account is finished in <paramref name="stored"/> becomes the stored one again
    /// (status, ids, address, text); the others are pending. Finished targets the edit left out are kept too. Orders are renumbered.
    /// </summary>
    public static List<PostTarget> KeepFinished(IReadOnlyList<PostTarget> edited, IReadOnlyList<PostTarget> stored)
    {
        ArgumentNullException.ThrowIfNull(edited);
        ArgumentNullException.ThrowIfNull(stored);
        var finished = new Dictionary<Guid, PostTarget>();
        foreach (var target in stored.Where(IsFinished))
        {
            finished.TryAdd(target.AccountId, target);
        }

        var result = new List<PostTarget>(edited.Count + finished.Count);
        foreach (var target in edited)
        {
            if (finished.Remove(target.AccountId, out var kept))
            {
                result.Add(kept);
                continue;
            }

            target.Status = TargetStatus.Pending;
            target.ExternalId = null;
            target.Url = null;
            target.Message = null;
            target.PublishedAtUtc = null;
            result.Add(target);
        }

        result.AddRange(stored.Where(t => finished.TryGetValue(t.AccountId, out var left) && ReferenceEquals(left, t)));
        for (var i = 0; i < result.Count; i++)
        {
            result[i].Order = i;
        }

        return result;
    }
}
