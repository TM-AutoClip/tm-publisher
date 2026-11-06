namespace TMPublisher.Core.Instance;

/// <summary>
/// Whether TM Publisher 0.1.0–0.1.2 runs for this data folder. Those versions sent the posts from their own window process and do not claim a
/// post before sending it, so nothing is claimed while one of them runs (a post could go out twice).
/// </summary>
public interface IOldVersionCheck
{
    /// <summary><c>true</c> while a 0.1.0–0.1.2 process holds its single-instance name for the data folder.</summary>
    bool OldVersionRuns();
}
