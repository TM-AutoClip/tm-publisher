namespace TMPublisher.Core.Publishing;

/// <summary>A posting limit or rate limit was reached.</summary>
public sealed class PlatformQuotaException : Exception
{
    /// <summary>Creates.</summary>
    public PlatformQuotaException()
    {
    }

    /// <summary>With a message.</summary>
    public PlatformQuotaException(string message)
        : base(message)
    {
    }

    /// <summary>With a message and the inner error.</summary>
    public PlatformQuotaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
