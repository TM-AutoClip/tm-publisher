using System.Globalization;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Connections;

/// <summary>
/// Browser sign-in that comes back to <c>http://127.0.0.1:{port}/callback</c> on this computer, with PKCE (S256) and a state value. The
/// port is fixed per platform so the user can register the exact address in the own developer app. The platform writes the sign-in
/// address, the code exchange and the identity read.
/// </summary>
public abstract class LoopbackPkceConnectionBase(HttpTransport transport, TimeProvider clock, ILoopbackListener listener) : ConnectionMethodBase(transport, clock)
{
    /// <summary>How long the browser sign-in may take.</summary>
    private static readonly TimeSpan SignInLimit = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    public override ConnectionKind Kind => ConnectionKind.LoopbackPkce;

    /// <summary>The fixed loopback port of this platform.</summary>
    protected abstract int Port { get; }

    /// <summary>Whether the app's client secret is needed (Google desktop clients and TikTok: yes; X public clients: no).</summary>
    protected virtual bool NeedsSecret => true;

    /// <summary>The return address to register.</summary>
    public string RedirectUri => "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture) + LoopbackListener.CallbackPath;

    /// <inheritdoc />
    public override IReadOnlyList<ConnectionField> Fields =>
    [
        new(CredentialKeys.RedirectUri, "field.redirect_uri", DefaultValue: RedirectUri, ReadOnly: true),
        new(CredentialKeys.ClientId, "field.client_id"),
        .. NeedsSecret ? new[] { new ConnectionField(CredentialKeys.ClientSecret, "field.client_secret", Secret: true) } : [],
    ];

    /// <inheritdoc />
    protected override async Task<Credentials> AuthorizeAsync(ConnectionInput input, IConnectionPrompt prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(prompt);
        var app = FromInput(input, CredentialKeys.ClientId, CredentialKeys.ClientSecret).With((CredentialKeys.RedirectUri, RedirectUri));
        var pkce = Pkce.Create();
        var state = Pkce.NewState();
        ILoopbackSession session;
        try
        {
            session = listener.Start(Port);
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            throw new PlatformRejectedException($"Port {Port} on this computer is in use by another program; close it and try again.", ex);
        }

        using (session)
        {
            prompt.Status("connect.waiting_browser");
            await prompt.OpenBrowserAsync(AuthorizeUri(app, state, Challenge(pkce)), cancellationToken).ConfigureAwait(false);
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(SignInLimit);
            var callback = await session.WaitAsync(limit.Token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(callback.Error))
            {
                throw new PlatformRejectedException("The sign-in was not completed: " + PlatformText.Short(callback.Error));
            }

            if (callback.State != state || string.IsNullOrEmpty(callback.Code))
            {
                throw new PlatformRejectedException("The sign-in answer did not match this request (state); try again.");
            }

            prompt.Status("connect.exchanging");
            return await ExchangeAsync(app, callback.Code, pkce.Verifier, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The code challenge sent in the sign-in address (S256, base64url); TikTok's desktop flow wants hex instead.</summary>
    protected virtual string Challenge(Pkce pkce)
    {
        ArgumentNullException.ThrowIfNull(pkce);
        return pkce.Challenge;
    }

    /// <summary>The platform's sign-in address.</summary>
    protected abstract Uri AuthorizeUri(Credentials app, string state, string challenge);

    /// <summary>Exchanges the code for tokens; returns the app values plus the tokens.</summary>
    protected abstract Task<Credentials> ExchangeAsync(Credentials app, string code, string verifier, CancellationToken cancellationToken);
}
