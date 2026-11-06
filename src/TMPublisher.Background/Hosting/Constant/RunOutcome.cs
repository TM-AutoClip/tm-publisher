namespace TMPublisher.Background.Hosting;

/// <summary>How a run ended (the log says the details; only <see cref="Failed"/> is a non-zero exit code).</summary>
public enum RunOutcome
{
    /// <summary>The due posts were handled (sent, blocked, stopped or missed) or nothing was due; the next start is set.</summary>
    Done,

    /// <summary>Another sender works on the data folder (the open window or another run): nothing was done.</summary>
    OtherSender,

    /// <summary>There is no database yet (the first start in the window is not finished).</summary>
    NoDatabase,

    /// <summary>The database is protected with a master password, which a run cannot ask for: nothing was sent.</summary>
    Locked,

    /// <summary>The run failed (the error is in the log); the next start is set with a growing wait.</summary>
    Failed,
}
