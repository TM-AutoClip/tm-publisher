namespace TMPublisher.Core.Platforms;

/// <summary>A known posting limit: at most <paramref name="MaxPosts"/> in the window.</summary>
public sealed record QuotaRule(int MaxPosts, QuotaWindow Window);
