namespace TMPublisher.Core.Accounts;

/// <summary>Whether an account can publish. Stored as a number: new values are appended at the end.</summary>
public enum AccountState
{
    /// <summary>Connected.</summary>
    Active = 0,

    /// <summary>The platform rejected the token; the user must connect again.</summary>
    NeedsReconnect = 1,
}
