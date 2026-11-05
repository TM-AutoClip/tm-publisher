using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Platforms.Common;
using TMPublisher.Core.Publishing;

namespace TMPublisher.Platforms.Connections;

/// <summary>
/// Browser sign-in for platforms that require an https return address (Threads, Pinterest): the user registers an https address in the own
/// app (any address; the page does not need to load), signs in, and pastes the address the browser ended on. The code and state are read
/// from it. The platform writes the sign-in address, the exchange and the identity read.
/// </summary>
public abstract class PastedRedirectConnectionBase(HttpTransport transport, TimeProvider clock) : ConnectionMethodBase(transport, clock)
{
    /// <summary>Suggested return address (the user may register another https address).</summary>
    public const string SuggestedRedirect = "https://localhost/callback";

    /// <inheritdoc />
    public override ConnectionKind Kind => ConnectionKind.PastedRedirect;

    /// <inheritdoc />
    public override IReadOnlyList<ConnectionField> Fields =>
    [
        new(CredentialKeys.ClientId, "field.client_id"),
        new(CredentialKeys.ClientSecret, "field.client_secret", Secret: true),
        new(CredentialKeys.RedirectUri, "field.redirect_https", DefaultValue: SuggestedRedirect),
    ];

    /// <inheritdoc />
    protected override async Task<Credentials> AuthorizeAsync(ConnectionInput input, IConnectionPrompt prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(prompt);
        var app = FromInput(input, CredentialKeys.ClientId, CredentialKeys.ClientSecret, CredentialKeys.RedirectUri);
        if (!Uri.TryCreate(app[CredentialKeys.RedirectUri], UriKind.Absolute, out var redirect) || redirect.Scheme != Uri.UriSchemeHttps)
        {
            throw new PlatformRejectedException("The return address must be an https address registered in your app.");
        }

        var state = Pkce.NewState();
        await prompt.OpenBrowserAsync(AuthorizeUri(app, state), cancellationToken).ConfigureAwait(false);
        var pasted = await prompt.AskPasteAsync("connect.paste_redirect", cancellationToken).ConfigureAwait(false);
        if (!Uri.TryCreate(pasted.Trim(), UriKind.Absolute, out var returned))
        {
            throw new PlatformRejectedException("The pasted text is not an address; copy the whole address from the browser's address bar.");
        }

        var query = LoopbackListener.Parse(returned.Query);
        if ((query.GetValueOrDefault("error_description") ?? query.GetValueOrDefault("error")) is { Length: > 0 } error)
        {
            throw new PlatformRejectedException("The sign-in was not completed: " + error);
        }

        if (query.GetValueOrDefault("state") != state || query.GetValueOrDefault("code") is not { Length: > 0 } code)
        {
            throw new PlatformRejectedException("The pasted address is not the answer of this sign-in (code or state missing); try again.");
        }

        // Some platforms append "#_" to the code in the address bar.
        prompt.Status("connect.exchanging");
        return await ExchangeAsync(app, code.Split('#')[0], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The platform's sign-in address.</summary>
    protected abstract Uri AuthorizeUri(Credentials app, string state);

    /// <summary>Exchanges the code for tokens.</summary>
    protected abstract Task<Credentials> ExchangeAsync(Credentials app, string code, CancellationToken cancellationToken);
}
