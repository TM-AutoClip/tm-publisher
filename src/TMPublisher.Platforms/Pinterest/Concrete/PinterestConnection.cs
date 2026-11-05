using System.Text.Json.Nodes;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.Pinterest;

/// <summary>
/// Pinterest sign-in with the user's own app (https return address pasted back), code exchange with Basic authorization; the board pins go
/// to is chosen here by name or id (the first board when empty) and checked to exist.
/// </summary>
public sealed class PinterestConnection(HttpTransport transport, TimeProvider clock) : PastedRedirectConnectionBase(transport, clock)
{
    /// <inheritdoc />
    public override IReadOnlyList<ConnectionField> Fields =>
    [
        .. base.Fields,
        new(PinterestApi.BoardKey, "field.pinterest_board", Required: false, HintKey: "field.pinterest_board.hint"),
    ];

    /// <inheritdoc />
    protected override Uri AuthorizeUri(Credentials app, string state)
    {
        ArgumentNullException.ThrowIfNull(app);
        return new Uri(PinterestApi.Authorize + "?response_type=code&client_id=" + Uri.EscapeDataString(app[CredentialKeys.ClientId])
            + "&redirect_uri=" + Uri.EscapeDataString(app[CredentialKeys.RedirectUri]) + "&scope=" + Uri.EscapeDataString(PinterestApi.Scopes)
            + "&state=" + Uri.EscapeDataString(state));
    }

    /// <inheritdoc />
    protected override async Task<Credentials> ExchangeAsync(Credentials app, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        var answer = await Http.PostFormAsync("Pinterest sign-in", PinterestApi.Root + "oauth/token",
            [new("grant_type", "authorization_code"), new("code", code), new("redirect_uri", app[CredentialKeys.RedirectUri])],
            PlatformHttp.Basic(app[CredentialKeys.ClientId], app[CredentialKeys.ClientSecret]), cancellationToken).ConfigureAwait(false);
        return OAuthTokens.Apply(app, answer, Clock.GetUtcNow());
    }

    /// <inheritdoc />
    protected override async Task<Credentials> AuthorizeAsync(ConnectionInput input, IConnectionPrompt prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tokens = await base.AuthorizeAsync(input, prompt, cancellationToken).ConfigureAwait(false);
        var boards = await Http.GetJsonAsync("Pinterest boards", PinterestApi.Root + "boards?page_size=100", tokens.Require(CredentialKeys.AccessToken), cancellationToken)
            .ConfigureAwait(false);
        var items = boards?["items"] as JsonArray ?? [];
        var wanted = input[PinterestApi.BoardKey];
        var board = wanted.Length == 0
            ? items.FirstOrDefault()
            : items.FirstOrDefault(b => JsonSafe.Str(b, "id") == wanted || string.Equals(JsonSafe.Str(b, "name"), wanted, StringComparison.OrdinalIgnoreCase));
        return board is null
            ? throw new PlatformRejectedException(items.Count == 0
                ? "This Pinterest account has no board; create one first."
                : "No board named '" + wanted + "'. Boards: " + string.Join(", ", items.Select(b => JsonSafe.Str(b, "name"))))
            : tokens.With((PinterestApi.BoardKey, JsonSafe.Str(board, "id")));
    }

    /// <inheritdoc />
    protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var me = await Http.GetJsonAsync("Pinterest account", PinterestApi.Root + "user_account", credentials.Require(CredentialKeys.AccessToken), cancellationToken)
            .ConfigureAwait(false);
        var username = JsonSafe.Str(me, "username");
        return new AccountIdentity(JsonSafe.Str(me, "id") is { Length: > 0 } id ? id : username, username, "@" + username);
    }
}
