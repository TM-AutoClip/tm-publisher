namespace TMPublisher.Storage.Database;

/// <summary>One publish attempt.</summary>
public sealed class PublishLogRow
{
    /// <summary>Row id.</summary>
    public long Id { get; set; }

    /// <summary>Account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Platform code.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Post.</summary>
    public Guid PostId { get; set; }

    /// <summary>When (UTC).</summary>
    public DateTimeOffset AtUtc { get; set; }

    /// <summary>Whether it succeeded.</summary>
    public bool Success { get; set; }
}
