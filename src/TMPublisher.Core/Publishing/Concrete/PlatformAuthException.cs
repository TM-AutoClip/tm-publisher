namespace TMPublisher.Core.Publishing;

/// <summary>The platform rejected the credentials; the account must be connected again.</summary>
public sealed class PlatformAuthException : Exception
{
    /// <summary>Creates.</summary>
    public PlatformAuthException()
    {
    }

    /// <summary>With a message.</summary>
    public PlatformAuthException(string message)
        : base(message)
    {
    }

    /// <summary>With a message and the inner error.</summary>
    public PlatformAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
