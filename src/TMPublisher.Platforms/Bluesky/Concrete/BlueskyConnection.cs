using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.Bluesky;

/// <summary>Handle + app password + server; the test reads the session (handle, DID).</summary>
public sealed class BlueskyConnection(HttpTransport transport, TimeProvider clock, BlueskySessions sessions) : AppPasswordConnectionBase(transport, clock)
{
    /// <inheritdoc />
    public override IReadOnlyList<ConnectionField> Fields =>
    [
        new(CredentialKeys.Handle, "field.bluesky_handle"),
        new(CredentialKeys.AppPassword, "field.app_password", Secret: true),
        new(CredentialKeys.Server, "field.server", DefaultValue: BlueskySessions.DefaultServer),
    ];

    /// <inheritdoc />
    protected override Task<Credentials> CreateSessionAsync(ConnectionInput input, CancellationToken cancellationToken) =>
        sessions.CreateAsync(FromInput(input, CredentialKeys.Handle, CredentialKeys.AppPassword, CredentialKeys.Server), cancellationToken);

    /// <inheritdoc />
    public override async Task<(AccountIdentity Identity, Credentials Credentials)> TestAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        try
        {
            return (await ReadIdentityAsync(credentials, cancellationToken).ConfigureAwait(false), credentials);
        }
        catch (Core.Publishing.PlatformAuthException)
        {
            var fresh = await sessions.RefreshAsync(credentials, cancellationToken).ConfigureAwait(false);
            return (await ReadIdentityAsync(fresh, cancellationToken).ConfigureAwait(false), fresh);
        }
    }

    /// <inheritdoc />
    protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var session = await Http.GetJsonAsync("Bluesky session", BlueskySessions.Server(credentials) + "/xrpc/com.atproto.server.getSession",
            credentials.Require(CredentialKeys.AccessToken), cancellationToken).ConfigureAwait(false);
        var handle = JsonSafe.Str(session, "handle");
        return new AccountIdentity(JsonSafe.Str(session, "did"), handle, "@" + handle);
    }
}
