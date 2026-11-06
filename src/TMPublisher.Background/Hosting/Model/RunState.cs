namespace TMPublisher.Background.Hosting;

/// <summary>What one run leaves for the next (<c>run-state.json</c> in the data folder, no secrets): the errors in a row and the last notices.</summary>
public sealed class RunState
{
    /// <summary>Runs in a row that failed (the wait before the next start grows with it; reset by a run that worked).</summary>
    public int Failures { get; set; }

    /// <summary>The post time the "open TM Publisher to unlock" notice was shown for (UTC); shown once per due post.</summary>
    public DateTimeOffset? UnlockNoticeForUtc { get; set; }
}
