using TMPublisher.Platforms.Connections;

namespace TMPublisher.Tests.Platforms;

/// <summary>A loopback listener that "receives" the browser's answer: the code given, and the state taken from the opened sign-in address.</summary>
internal sealed class FakeLoopback(FakePrompt prompt, string code = "the-code") : ILoopbackListener
{
    public int? Port { get; private set; }

    public ILoopbackSession Start(int port)
    {
        Port = port;
        return new Session(prompt, code);
    }

    /// <summary>One session.</summary>
    private sealed class Session(FakePrompt prompt, string code) : ILoopbackSession
    {
        public Task<LoopbackCallback> WaitAsync(CancellationToken cancellationToken)
        {
            var state = LoopbackListener.Parse(prompt.Opened.Last().Query).GetValueOrDefault("state");
            return Task.FromResult(new LoopbackCallback(code, state, null));
        }

        public void Dispose()
        {
        }
    }
}
