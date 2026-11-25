using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Paths;
using TMPublisher.Platforms.Common;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Desktop.Updates;

/// <summary>
/// Asks GitHub for the newest published release (<see cref="UpdateRules.Latest"/>, one request, no token) and compares it with this program's
/// version. The last answer is kept in <see cref="UpdateRules.StateFileName"/>: its ETag goes with the next request, so an unchanged answer
/// (304) costs nothing of GitHub's hourly limit. A rate-limit answer (403 / 429 with no requests left, or Retry-After) is respected until the
/// time GitHub names, also by "Check now"; offline or refused, the last known answer is used and the reason is logged. Nothing is downloaded
/// or installed: the window only says that a new version exists and opens its download page.
/// </summary>
public sealed class ReleaseChecker(HttpTransport transport, AppPaths paths, TimeProvider clock, ILogger<ReleaseChecker> logger)
{
    private readonly string _stateFile = Path.Combine(paths.DataDirectory, UpdateRules.StateFileName);

    /// <summary>This program's version (major.minor.patch).</summary>
    public static Version Current { get; } = Normalize(typeof(ReleaseChecker).Assembly.GetName().Version ?? new Version(0, 0, 0));

    /// <summary>Looks once (never throws for network problems; a cancel by <paramref name="cancellationToken"/> is passed on).</summary>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        var state = ReadState();
        var now = clock.GetUtcNow();
        if (state.NotBeforeUtc is { } notBefore && now < notBefore)
        {
            logger.LogInformation("Update check: GitHub asked to wait until {Time:u}; nothing is sent before that", notBefore);
            return Known(state, UpdateOutcome.Waiting, notBefore);
        }

        try
        {
            using var client = transport.CreateClient(UpdateRules.RequestTimeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, UpdateRules.Latest);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", UpdateRules.ApiVersion);
            if (state.LatestTag is not null && state.ETag is { Length: > 0 } etag && EntityTagHeaderValue.TryParse(etag, out var tag))
            {
                request.Headers.IfNoneMatch.Add(tag);
            }

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified && state.LatestTag is not null)
            {
                state.CheckedAtUtc = now;
                state.NotBeforeUtc = null;
                SaveState(state);
                logger.LogInformation("Update check: unchanged, the newest release is {Tag} (this is {Current})", state.LatestTag, Current);
                return Known(state, null, null);
            }

            if (Limited(response, now) is { } wait)
            {
                state.NotBeforeUtc = wait;
                SaveState(state);
                logger.LogWarning("Update check: GitHub's limit for requests without a token is reached; next look after {Time:u}", wait);
                return Known(state, UpdateOutcome.Waiting, wait);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // No published release (only drafts or pre-releases, or none at all).
                state.CheckedAtUtc = now;
                state.LatestTag = state.LatestPage = state.ETag = null;
                state.NotBeforeUtc = null;
                SaveState(state);
                logger.LogInformation("Update check: no published release");
                return new UpdateCheckResult(UpdateOutcome.NoRelease, null, UpdateRules.DownloadPage);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Update check: GitHub answered HTTP {Status}; the last known answer is used", (int)response.StatusCode);
                return Known(state, UpdateOutcome.Unreachable, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var release = JsonSerializer.Deserialize(body, DesktopJsonContext.Default.GitHubRelease);
            if (release is null || release.Draft || release.Prerelease || ParseTag(release.Tag) is null)
            {
                logger.LogWarning("Update check: GitHub's answer names no usable release (tag '{Tag}')", release?.Tag ?? "(none)");
                return Known(state, UpdateOutcome.Unreachable, null);
            }

            state.CheckedAtUtc = now;
            state.NotBeforeUtc = null;
            state.ETag = response.Headers.ETag?.ToString();
            state.LatestTag = release.Tag;
            state.LatestPage = release.Page;
            SaveState(state);
            var result = Known(state, null, null);
            logger.LogInformation("Update check: the newest release is {Tag} (this is {Current}): {Outcome}", release.Tag, Current, result.Outcome);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException
            or TaskCanceledException { CancellationToken.IsCancellationRequested: false })
        {
            logger.LogWarning("Update check: GitHub could not be reached ({Type}); the last known answer is used", ex.GetType().Name);
            return Known(state, UpdateOutcome.Unreachable, null);
        }
    }

    /// <summary>The version of a release tag (<c>v1.2.3</c> or <c>1.2.3</c>); null for a pre-release tag (<c>-beta</c>) or anything else.</summary>
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var text = tag.Trim().TrimStart('v', 'V');
        if (text.Contains('-', StringComparison.Ordinal) || text.Contains('+', StringComparison.Ordinal) || text.Count(c => c == '.') is < 1 or > 2)
        {
            return null;
        }

        return Version.TryParse(text, out var version) ? Normalize(version) : null;
    }

    /// <summary>
    /// The result for a known newest release: <see cref="UpdateOutcome.NewVersion"/> when it is newer than <paramref name="current"/>, otherwise
    /// <see cref="UpdateOutcome.UpToDate"/>; its own page only when it is a release page of this repository.
    /// </summary>
    public static UpdateCheckResult Compare(string? tag, string? page, Version current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var latest = ParseTag(tag);
        var address = page is not null && page.StartsWith(UpdateRules.PagePrefix, StringComparison.Ordinal)
            && Uri.TryCreate(page, UriKind.Absolute, out var own) ? own : UpdateRules.DownloadPage;
        return latest is null
            ? new UpdateCheckResult(UpdateOutcome.NoRelease, null, UpdateRules.DownloadPage)
            : new UpdateCheckResult(latest > Normalize(current) ? UpdateOutcome.NewVersion : UpdateOutcome.UpToDate, latest, address);
    }

    /// <summary>The last kept answer (empty when there is none or it cannot be read).</summary>
    public UpdateCheckState ReadState()
    {
        if (!File.Exists(_stateFile))
        {
            return new UpdateCheckState();
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(_stateFile), DesktopJsonContext.Default.UpdateCheckState) ?? new UpdateCheckState();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The last update check could not be read; it starts again");
            return new UpdateCheckState();
        }
    }

    /// <summary>
    /// The kept answer compared with this version. With an <paramref name="outcome"/> (waiting, unreachable) a known newer release is still
    /// reported as new, so the window keeps offering it while GitHub cannot be asked.
    /// </summary>
    private static UpdateCheckResult Known(UpdateCheckState state, UpdateOutcome? outcome, DateTimeOffset? notBefore)
    {
        var known = Compare(state.LatestTag, state.LatestPage, Current);
        if (outcome is not { } other || known.Outcome == UpdateOutcome.NewVersion)
        {
            return known with { NotBeforeUtc = notBefore };
        }

        return known with { Outcome = other, NotBeforeUtc = notBefore };
    }

    private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));

    /// <summary>When a rate-limit answer allows the next request; null when the answer is not one.</summary>
    private static DateTimeOffset? Limited(HttpResponseMessage response, DateTimeOffset now)
    {
        if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests))
        {
            return null;
        }

        if (response.Headers.RetryAfter is { } retry)
        {
            return retry.Date ?? now + (retry.Delta ?? TimeSpan.FromMinutes(1));
        }

        var remaining = Header(response, "X-RateLimit-Remaining");
        if (remaining == "0" && long.TryParse(Header(response, "X-RateLimit-Reset"), NumberStyles.None, CultureInfo.InvariantCulture, out var reset))
        {
            return DateTimeOffset.FromUnixTimeSeconds(reset);
        }

        // A 403/429 without the headers is still a refusal: wait rather than ask again at once.
        return response.StatusCode == HttpStatusCode.TooManyRequests || remaining == "0" ? now + UpdateRules.RefusedWait : null;
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private void SaveState(UpdateCheckState state)
    {
        try
        {
            var temporary = _stateFile + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, DesktopJsonContext.Default.UpdateCheckState));
            File.Move(temporary, _stateFile, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The update check could not be kept; the next start asks again");
        }
    }
}
