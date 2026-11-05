namespace TMPublisher.Core.Platforms;

/// <summary>Every registered platform, by code.</summary>
public interface IPlatformDirectory
{
    /// <summary>All platforms in display order.</summary>
    IReadOnlyList<IPlatform> All { get; }

    /// <summary>The platform; <c>null</c> when the code is unknown.</summary>
    IPlatform? Find(string code);

    /// <summary>The platform, or an exception for an unknown code.</summary>
    IPlatform Get(string code);
}
