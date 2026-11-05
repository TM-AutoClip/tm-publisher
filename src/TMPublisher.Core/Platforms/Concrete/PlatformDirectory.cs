namespace TMPublisher.Core.Platforms;

/// <summary>Platform directory built from every <see cref="IPlatform"/> registered in DI.</summary>
public sealed class PlatformDirectory : IPlatformDirectory
{
    private readonly Dictionary<string, IPlatform> _byCode;

    /// <summary>Builds the directory; duplicate codes are a programming error.</summary>
    public PlatformDirectory(IEnumerable<IPlatform> platforms)
    {
        ArgumentNullException.ThrowIfNull(platforms);
        All = platforms.ToList();
        _byCode = new Dictionary<string, IPlatform>(StringComparer.Ordinal);
        foreach (var platform in All)
        {
            if (!_byCode.TryAdd(platform.Code, platform))
            {
                throw new InvalidOperationException($"Platform code '{platform.Code}' is registered twice.");
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IPlatform> All { get; }

    /// <inheritdoc />
    public IPlatform? Find(string code) => _byCode.GetValueOrDefault(code);

    /// <inheritdoc />
    public IPlatform Get(string code) => Find(code) ?? throw new KeyNotFoundException($"Unknown platform '{code}'.");
}
