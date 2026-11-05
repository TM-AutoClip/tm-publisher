using System.Buffers.Text;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.Bluesky;

/// <summary>
/// Bluesky sessions: createSession with the app password, refreshSession with the refresh token; when the refresh token is no longer
/// accepted a new session is created with the stored app password (that is what app passwords are for). The access token's expiry is read
/// from its JWT.
/// </summary>
public sealed class BlueskySessions(HttpTransport transport, TimeProvider clock) : ITokenRefresher
{
    /// <summary>Default server.</summary>
    public const string DefaultServer = "https://bsky.social";

    private readonly PlatformHttp _http = new(transport.CreateClient(TimeSpan.FromSeconds(60)));

    /// <inheritdoc />
    public string Platform => Core.Platforms.PlatformCodes.Bluesky;

    /// <summary>Server address of credentials (without a trailing slash).</summary>
    public static string Server(Credentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var server = credentials[CredentialKeys.Server];
        return (server.Length > 0 ? server : DefaultServer).TrimEnd('/');
    }

    /// <inheritdoc />
    public bool NeedsRefresh(Credentials credentials, DateTimeOffset nowUtc) => OAuthTokens.ExpiresSoon(credentials, nowUtc);

    /// <summary>A new session with the app password.</summary>
    public async Task<Credentials> CreateAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var answer = await _http.PostJsonAsync("Bluesky sign-in", Server(credentials) + "/xrpc/com.atproto.server.createSession",
            new JsonObject { ["identifier"] = credentials.Require(CredentialKeys.Handle), ["password"] = credentials.Require(CredentialKeys.AppPassword) },
            null, cancellationToken).ConfigureAwait(false);
        return Apply(credentials, answer);
    }

    /// <inheritdoc />
    public async Task<Credentials> RefreshAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        using var request = new HttpRequestMessage(HttpMethod.Post, Server(credentials) + "/xrpc/com.atproto.server.refreshSession");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Require(CredentialKeys.RefreshToken));
        var reply = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return reply.IsSuccess
            ? Apply(credentials, JsonSafe.TryParse(reply.Body))
            : await CreateAsync(credentials, cancellationToken).ConfigureAwait(false);
    }

    private Credentials Apply(Credentials credentials, JsonNode? answer)
    {
        var access = JsonSafe.Str(answer, "accessJwt");
        if (access.Length == 0)
        {
            throw new PlatformAuthException("Bluesky did not open a session; check the handle and the app password.");
        }

        return credentials.With(
            (CredentialKeys.AccessToken, access),
            (CredentialKeys.RefreshToken, JsonSafe.Str(answer, "refreshJwt")),
            (CredentialKeys.UserId, JsonSafe.Str(answer, "did")),
            (CredentialKeys.AccessExpiresAt, JwtExpiry(access) ?? clock.GetUtcNow().AddHours(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>The <c>exp</c> claim of a JWT (Unix seconds); <c>null</c> when unreadable.</summary>
    internal static string? JwtExpiry(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = JsonSafe.TryParse(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[1])));
            return JsonSafe.Str(payload, "exp") is { Length: > 0 } exp ? exp : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
