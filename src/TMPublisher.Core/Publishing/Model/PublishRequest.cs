using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;

namespace TMPublisher.Core.Publishing;

/// <summary>
/// Everything a publisher needs for one target. <paramref name="Renew"/> refreshes the credentials once when the platform rejects the
/// token (and stores the new ones before returning); <c>null</c> when the platform has no refresh.
/// </summary>
public sealed record PublishRequest(
    Account Account,
    Credentials Credentials,
    Post Post,
    PostTarget Target,
    Func<CancellationToken, Task<Credentials>>? Renew);
