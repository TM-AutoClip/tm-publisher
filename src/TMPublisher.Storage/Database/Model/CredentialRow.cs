namespace TMPublisher.Storage.Database;

/// <summary>Secrets of one account (own table; JSON of the values).</summary>
public sealed class CredentialRow
{
    /// <summary>Account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Values as JSON.</summary>
    public string Json { get; set; } = "{}";
}
