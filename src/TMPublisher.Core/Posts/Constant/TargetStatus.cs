namespace TMPublisher.Core.Posts;

/// <summary>State of one target (one account) of a post. Stored as a number: new values are appended at the end.</summary>
public enum TargetStatus
{
    /// <summary>Not sent yet.</summary>
    Pending = 0,

    /// <summary>Upload running.</summary>
    Publishing = 1,

    /// <summary>Uploaded; the platform is still processing it.</summary>
    Processing = 2,

    /// <summary>Sent as a draft / to the inbox; the user must approve it in the platform's app (TikTok).</summary>
    NeedsApproval = 3,

    /// <summary>Published.</summary>
    Published = 4,

    /// <summary>Failed; the error is stored.</summary>
    Failed = 5,

    /// <summary>Skipped by the user after a failure.</summary>
    Skipped = 6,

    /// <summary>Canceled before it was sent.</summary>
    Canceled = 7,
}
