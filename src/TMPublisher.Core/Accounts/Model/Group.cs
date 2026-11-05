namespace TMPublisher.Core.Accounts;

/// <summary>A named, ordered list of accounts a post can be sent to.</summary>
public sealed class Group
{
    /// <summary>Id.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Accounts in sending order.</summary>
    public List<Guid> AccountIds { get; set; } = [];
}
