using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Background.Os;

/// <summary>
/// Linux: the main thread waits until <see cref="Quit"/>. SIGTERM (log out, shut down, <c>kill</c>) and SIGINT (Ctrl+C) ask for a clean stop;
/// SIGHUP is ignored, so closing the terminal the scheduler was started from does not end it.
/// </summary>
public sealed class SignalMainLoop(ILogger<SignalMainLoop> logger) : IMainLoop, IDisposable
{
    private readonly ManualResetEventSlim _done = new();
    private readonly List<PosixSignalRegistration> _signals = [];

    /// <inheritdoc />
    public event EventHandler? StopRequested;

    /// <inheritdoc />
    public void Run()
    {
        _signals.Add(PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnStop));
        _signals.Add(PosixSignalRegistration.Create(PosixSignal.SIGINT, OnStop));
        _signals.Add(PosixSignalRegistration.Create(PosixSignal.SIGHUP, context =>
        {
            context.Cancel = true;
            logger.LogInformation("SIGHUP ignored (the terminal closed); the scheduler keeps running");
        }));
        _done.Wait();
        logger.LogInformation("Main loop ended");
    }

    /// <inheritdoc />
    public void Quit() => _done.Set();

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var signal in _signals)
        {
            signal.Dispose();
        }

        _done.Dispose();
    }

    private void OnStop(PosixSignalContext context)
    {
        // The stop is done by the host (finish or interrupt the current post, then Quit): the default handler would end the process at once.
        context.Cancel = true;
        logger.LogInformation("{Signal} received; stopping", context.Signal);
        StopRequested?.Invoke(this, EventArgs.Empty);
    }
}
