namespace TMPublisher.Core.Platforms;

/// <summary>How a platform counts its posting limit.</summary>
public enum QuotaWindow
{
    /// <summary>The last 24 hours.</summary>
    Rolling24Hours = 0,

    /// <summary>Calendar day in Pacific Time (YouTube resets at midnight PT).</summary>
    PacificDay = 1,

    /// <summary>The last 15 minutes (X).</summary>
    Rolling15Minutes = 2,
}
