using TMPublisher.Core.Posts;

namespace TMPublisher.Core.Publishing;

/// <summary>
/// Outcome of a successful publish: the platform's id, the address (may be empty) and the state (published, still processing, or waiting
/// for approval in the platform's app). <paramref name="NoteKey"/> is an optional localization key shown on the target row.
/// </summary>
public sealed record PublishResult(string ExternalId, string Url, TargetStatus State, string? NoteKey = null);
