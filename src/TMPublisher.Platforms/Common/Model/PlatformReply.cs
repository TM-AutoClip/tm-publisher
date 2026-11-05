namespace TMPublisher.Platforms.Common;

/// <summary>A platform's HTTP answer (body read, response closed); <paramref name="Headers"/> holds the first value of each header.</summary>
public readonly record struct PlatformReply(int Status, string Body, TimeSpan? RetryAfter, IReadOnlyDictionary<string, string>? Headers = null)
{
    /// <summary>2xx.</summary>
    public bool IsSuccess => Status is >= 200 and < 300;
}
