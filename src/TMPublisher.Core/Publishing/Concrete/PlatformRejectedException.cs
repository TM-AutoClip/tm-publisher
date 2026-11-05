namespace TMPublisher.Core.Publishing;

/// <summary>The platform (or a local check before any request) rejected the post; the message carries the reason.</summary>
public sealed class PlatformRejectedException : Exception
{
    /// <summary>Creates.</summary>
    public PlatformRejectedException()
    {
    }

    /// <summary>With a message.</summary>
    public PlatformRejectedException(string message)
        : base(message)
    {
    }

    /// <summary>With a message and the inner error.</summary>
    public PlatformRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
