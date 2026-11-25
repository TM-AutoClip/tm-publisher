namespace TMPublisher.Desktop.Updates;

/// <summary>What a look for a new version found.</summary>
public enum UpdateOutcome
{
    /// <summary>Not looked yet (or the look is still running).</summary>
    NotChecked,

    /// <summary>The newest published release is this version or older.</summary>
    UpToDate,

    /// <summary>A newer version is published.</summary>
    NewVersion,

    /// <summary>GitHub asked not to be asked again before a time (rate limit); nothing was sent.</summary>
    Waiting,

    /// <summary>GitHub could not be reached or refused the request.</summary>
    Unreachable,

    /// <summary>The repository has no published release.</summary>
    NoRelease,

    /// <summary>Looking at every start is turned off in Settings.</summary>
    Off,
}
