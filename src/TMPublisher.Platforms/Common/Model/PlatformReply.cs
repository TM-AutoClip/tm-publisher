namespace TMPublisher.Platforms.Common;

/// <summary>A platform's HTTP answer (body read, response closed).</summary>
public readonly record struct PlatformReply(int Status, string Body, TimeSpan? RetryAfter)
{
    /// <summary>2xx.</summary>
    public bool IsSuccess => Status is >= 200 and < 300;
}
