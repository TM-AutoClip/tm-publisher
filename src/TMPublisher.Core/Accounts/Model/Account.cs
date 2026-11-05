namespace TMPublisher.Core.Accounts;

/// <summary>A connected social account. Its secrets are stored apart (<see cref="ICredentialStore"/>).</summary>
public sealed class Account
{
    /// <summary>Id.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Platform code (<see cref="Platforms.PlatformCodes"/>).</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Name shown in the app.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Handle (<c>@name</c>, channel url...), when the platform has one.</summary>
    public string? Handle { get; set; }

    /// <summary>The platform's id of the account (channel id, user id, page id, chat id...).</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>State.</summary>
    public AccountState State { get; set; }

    /// <summary>When the access must be renewed by connecting again (<c>null</c>: never or refreshed automatically).</summary>
    public DateTimeOffset? ReconnectByUtc { get; set; }

    /// <summary>Connected (UTC).</summary>
    public DateTimeOffset ConnectedAtUtc { get; set; }
}
