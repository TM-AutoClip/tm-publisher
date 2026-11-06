namespace TMPublisher.Tests.Platforms;

/// <summary>A platform that never answers: every request waits until it is cancelled (the client's time-out or the caller). No network.</summary>
internal sealed class SilentHttp : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException("not reached");
    }
}
