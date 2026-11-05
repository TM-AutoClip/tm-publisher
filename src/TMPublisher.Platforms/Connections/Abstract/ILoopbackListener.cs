namespace TMPublisher.Platforms.Connections;

/// <summary>Listens on <c>http://127.0.0.1:port/callback</c> for one sign-in answer.</summary>
public interface ILoopbackListener
{
    /// <summary>Starts listening; an exception when the port is in use.</summary>
    ILoopbackSession Start(int port);
}
