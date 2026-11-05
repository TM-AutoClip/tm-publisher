using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Connections;

/// <summary>Shared part of every connection flow: fields, the required-field check, HTTP, and the identity read used as the connection test.</summary>
public abstract class ConnectionMethodBase(HttpTransport transport, TimeProvider clock) : IConnectionMethod
{
    /// <summary>HTTP.</summary>
    protected PlatformHttp Http { get; } = new(transport.CreateClient(TimeSpan.FromSeconds(60)));

    /// <summary>Clock.</summary>
    protected TimeProvider Clock { get; } = clock;

    /// <inheritdoc />
    public abstract ConnectionKind Kind { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<ConnectionField> Fields { get; }

    /// <inheritdoc />
    public async Task<ConnectionResult> ConnectAsync(ConnectionInput input, IConnectionPrompt prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(prompt);
        var missing = Fields.Where(f => f.Required && !f.ReadOnly && input[f.Key].Length == 0).Select(f => f.Key).ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException("Required values are missing: " + string.Join(", ", missing));
        }

        var credentials = await AuthorizeAsync(input, prompt, cancellationToken).ConfigureAwait(false);
        prompt.Status("connect.testing");
        var identity = await ReadIdentityAsync(credentials, cancellationToken).ConfigureAwait(false);
        return new ConnectionResult(identity, credentials.With((CredentialKeys.UserId, identity.ExternalId)), ReconnectBy(credentials));
    }

    /// <inheritdoc />
    public virtual async Task<(AccountIdentity Identity, Credentials Credentials)> TestAsync(Credentials credentials, CancellationToken cancellationToken) =>
        (await ReadIdentityAsync(credentials, cancellationToken).ConfigureAwait(false), credentials);

    /// <summary>The flow itself: turns the input into stored credentials.</summary>
    protected abstract Task<Credentials> AuthorizeAsync(ConnectionInput input, IConnectionPrompt prompt, CancellationToken cancellationToken);

    /// <summary>Reads who the credentials belong to (the connection test).</summary>
    protected abstract Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken);

    /// <summary>When the user must connect again (platforms without automatic renewal); default never.</summary>
    protected virtual DateTimeOffset? ReconnectBy(Credentials credentials) => null;

    /// <summary>The input's values that have a credential key of the same name.</summary>
    protected static Credentials FromInput(ConnectionInput input, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Credentials.Empty.With(keys.Select(k => (k, (string?)input[k])).ToArray());
    }
}
