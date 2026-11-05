using System.Globalization;
using System.Text.Json.Nodes;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Connections;

/// <summary>Reads an OAuth token answer into <see cref="Credentials"/> (access, refresh, expiry times as Unix seconds).</summary>
public static class OAuthTokens
{
    /// <summary>The credentials with the answer's tokens; the old refresh token is kept when the answer has none.</summary>
    public static Credentials Apply(Credentials existing, JsonNode? answer, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var access = JsonSafe.Str(answer, "access_token");
        if (access.Length == 0)
        {
            throw new PlatformRejectedException("The token answer has no access_token.");
        }

        var refresh = JsonSafe.Str(answer, "refresh_token");
        return existing.With(
            (CredentialKeys.AccessToken, access),
            (CredentialKeys.RefreshToken, refresh.Length > 0 ? refresh : existing[CredentialKeys.RefreshToken]),
            (CredentialKeys.AccessExpiresAt, Expiry(answer, "expires_in", nowUtc) ?? string.Empty),
            (CredentialKeys.RefreshExpiresAt, Expiry(answer, "refresh_expires_in", nowUtc) ?? Expiry(answer, "refresh_token_expires_in", nowUtc) ?? existing[CredentialKeys.RefreshExpiresAt]));
    }

    /// <summary>Unix seconds of now + the answer's seconds value; <c>null</c> when absent.</summary>
    public static string? Expiry(JsonNode? answer, string name, DateTimeOffset nowUtc) =>
        long.TryParse(JsonSafe.Str(answer, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? nowUtc.AddSeconds(seconds).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
            : null;

    /// <summary>Whether the access token expires within five minutes (or already did).</summary>
    public static bool ExpiresSoon(Credentials credentials, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        return credentials.AccessExpiresAtUtc is { } expiry && expiry - nowUtc < TimeSpan.FromMinutes(5);
    }
}
