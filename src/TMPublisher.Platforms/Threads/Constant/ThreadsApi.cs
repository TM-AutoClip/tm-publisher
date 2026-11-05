namespace TMPublisher.Platforms.Threads;

/// <summary>Threads API addresses (one version, v1.0).</summary>
public static class ThreadsApi
{
    /// <summary>Graph root with version.</summary>
    public const string Graph = "https://graph.threads.net/v1.0/";

    /// <summary>Token root (no version).</summary>
    public const string TokenRoot = "https://graph.threads.net/";

    /// <summary>Sign-in page.</summary>
    public const string Authorize = "https://threads.net/oauth/authorize";

    /// <summary>Permissions.</summary>
    public const string Scopes = "threads_basic,threads_content_publish";
}
