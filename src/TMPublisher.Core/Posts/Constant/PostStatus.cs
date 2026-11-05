namespace TMPublisher.Core.Posts;

/// <summary>State of a whole post. Stored as a number: new values are appended at the end.</summary>
public enum PostStatus
{
    /// <summary>Saved, not scheduled.</summary>
    Draft = 0,

    /// <summary>Waits for its time.</summary>
    Scheduled = 1,

    /// <summary>Being sent to its targets one after the other.</summary>
    Publishing = 2,

    /// <summary>Stopped at a failed target; the user decides: retry, skip and continue, or cancel the rest.</summary>
    Stopped = 3,

    /// <summary>Every target finished (published, processing, waiting for approval or skipped).</summary>
    Done = 4,

    /// <summary>Its time passed while the app was not running (or more than the tolerance late); the user decides.</summary>
    Missed = 5,

    /// <summary>Canceled by the user.</summary>
    Canceled = 6,

    /// <summary>The preflight check failed; nothing was sent.</summary>
    Blocked = 7,
}
