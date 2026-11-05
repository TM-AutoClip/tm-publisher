namespace TMPublisher.Platforms.Common;

/// <summary>
/// The access token of one publish: when the platform rejects it (401) a new one is taken once through the request's renew function (the
/// app refreshes with the user's own app and stores it); a second rejection means "connect again". The token lives in memory only.
/// </summary>
public sealed class TokenSession(string token, Func<CancellationToken, Task<string>>? renew)
{
    private bool _renewed;

    /// <summary>Current token.</summary>
    public string Token { get; private set; } = token;

    /// <summary>Takes a new token; <c>false</c> when it was already renewed or there is no renewal.</summary>
    public async Task<bool> RenewAsync(CancellationToken cancellationToken)
    {
        if (_renewed || renew is null)
        {
            return false;
        }

        _renewed = true;
        Token = await renew(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Renews or throws <see cref="Core.Publishing.PlatformAuthException"/> with the reason.</summary>
    public async Task RenewOrFailAsync(string reason, CancellationToken cancellationToken)
    {
        if (!await RenewAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new Core.Publishing.PlatformAuthException(reason);
        }
    }
}
