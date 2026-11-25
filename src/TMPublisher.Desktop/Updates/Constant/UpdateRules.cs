namespace TMPublisher.Desktop.Updates;

/// <summary>Where the window looks for a new version and how it asks (GitHub's public API, no token).</summary>
public static class UpdateRules
{
    /// <summary>The newest published release (GitHub leaves drafts and pre-releases out of this answer).</summary>
    public static readonly Uri Latest = new("https://api.github.com/repos/TM-AutoClip/tm-publisher/releases/latest");

    /// <summary>The download page shown to the user (a release's own page is used when GitHub names one under <see cref="PagePrefix"/>).</summary>
    public static readonly Uri DownloadPage = new("https://github.com/TM-AutoClip/tm-publisher/releases/latest");

    /// <summary>Only a release page of this repository is opened from the answer; anything else opens <see cref="DownloadPage"/>.</summary>
    public const string PagePrefix = "https://github.com/TM-AutoClip/tm-publisher/releases/";

    /// <summary>GitHub's REST API version.</summary>
    public const string ApiVersion = "2022-11-28";

    /// <summary>The whole look (one request) gives up after this long; the start is never waiting for it.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>A 403 / 429 without GitHub's reset time: no look before this long has passed.</summary>
    public static readonly TimeSpan RefusedWait = TimeSpan.FromHours(1);

    /// <summary>The file in the data folder that keeps the last answer and its ETag.</summary>
    public const string StateFileName = "update-check.json";
}
