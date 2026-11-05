using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.LinkedIn;

/// <summary>Pasted token from LinkedIn's token generator (scopes openid, profile, w_member_social); the test reads OpenID user info.</summary>
public sealed class LinkedInConnection(HttpTransport transport, TimeProvider clock) : PastedTokenConnectionBase(transport, clock)
{
    /// <summary>Lifetime of a generated token.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(60);

    /// <inheritdoc />
    public override IReadOnlyList<ConnectionField> Fields => [new(CredentialKeys.AccessToken, "field.linkedin_token", Secret: true)];

    /// <inheritdoc />
    protected override Task<Credentials> ConvertAsync(ConnectionInput input, CancellationToken cancellationToken) =>
        Task.FromResult(FromInput(input, CredentialKeys.AccessToken).With((CredentialKeys.AccessExpiresAt,
            Clock.GetUtcNow().Add(TokenLifetime).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture))));

    /// <inheritdoc />
    protected override DateTimeOffset? ReconnectBy(Credentials credentials) => credentials?.AccessExpiresAtUtc;

    /// <inheritdoc />
    protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var me = await Http.GetJsonAsync("LinkedIn profile", LinkedInApi.UserInfo, credentials.Require(CredentialKeys.AccessToken), cancellationToken).ConfigureAwait(false);
        return new AccountIdentity(JsonSafe.Str(me, "sub"), JsonSafe.Str(me, "name"), null);
    }
}
