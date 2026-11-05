namespace TMPublisher.Platforms.YouTube;

/// <summary>The resumable upload session is gone on the server (404/410): it does not resume, a new session starts from the beginning.</summary>
public sealed class UploadSessionLostException : Exception
{
    /// <summary>Creates.</summary>
    public UploadSessionLostException()
    {
    }

    /// <summary>With a message.</summary>
    public UploadSessionLostException(string message)
        : base(message)
    {
    }

    /// <summary>With a message and the inner error.</summary>
    public UploadSessionLostException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
