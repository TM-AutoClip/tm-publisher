namespace TMPublisher.Platforms.Connections;

/// <summary>One listening session.</summary>
public interface ILoopbackSession : IDisposable
{
    /// <summary>Waits for the first valid <c>GET /callback</c> and answers the browser with a short "you can close this tab" page.</summary>
    Task<LoopbackCallback> WaitAsync(CancellationToken cancellationToken);
}
