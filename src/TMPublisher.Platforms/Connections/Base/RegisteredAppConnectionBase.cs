using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Platforms.Common;
using TMPublisher.Core.Publishing;

namespace TMPublisher.Platforms.Connections;

/// <summary>
/// Servers where the app registers the user's own app automatically (Mastodon): register on the user's server, sign in in the browser, paste
/// the code shown there (out-of-band). The platform writes the registration, the sign-in address and the exchange.
/// </summary>
public abstract class RegisteredAppConnectionBase(HttpTransport transport, TimeProvider clock) : ConnectionMethodBase(transport, clock)
{
    /// <inheritdoc />
    public override ConnectionKind Kind => ConnectionKind.RegisteredApp;

    /// <inheritdoc />
    protected override async Task<Credentials> AuthorizeAsync(ConnectionInput input, IConnectionPrompt prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(prompt);
        prompt.Status("connect.registering");
        var app = await RegisterAsync(input, cancellationToken).ConfigureAwait(false);
        await prompt.OpenBrowserAsync(AuthorizeUri(app), cancellationToken).ConfigureAwait(false);
        var code = (await prompt.AskPasteAsync("connect.paste_code", cancellationToken).ConfigureAwait(false)).Trim();
        if (code.Length == 0)
        {
            throw new PlatformRejectedException("No code was pasted.");
        }

        prompt.Status("connect.exchanging");
        return await ExchangeAsync(app, code, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Registers the user's app on the server (client id and secret).</summary>
    protected abstract Task<Credentials> RegisterAsync(ConnectionInput input, CancellationToken cancellationToken);

    /// <summary>The server's sign-in address.</summary>
    protected abstract Uri AuthorizeUri(Credentials app);

    /// <summary>Exchanges the pasted code.</summary>
    protected abstract Task<Credentials> ExchangeAsync(Credentials app, string code, CancellationToken cancellationToken);
}
