namespace TMPublisher.Core.Settings;

/// <summary>Default tags of a platform: they fill the compose form, the user can still edit them per post.</summary>
public sealed class TagSet
{
    /// <summary>Platform code.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Tags (with or without '#').</summary>
    public List<string> Tags { get; set; } = [];
}
