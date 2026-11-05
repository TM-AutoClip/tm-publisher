using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using TMPublisher.Runtime.Instance;

namespace TMPublisher.Desktop.Instance;

/// <summary>
/// One window per data folder: the first start holds <c>window-instance.lock</c> in the data folder (<see cref="InstanceLock"/>; a named mutex is
/// scoped to the login session on Linux), a named pipe (a Unix socket on Linux) carries "show your window" from a second start to the first.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly InstanceLock? _lock;
    private readonly string _name;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(InstanceLock? held, string name)
    {
        _lock = held;
        _name = name;
        if (IsFirst)
        {
            _ = ListenAsync();
        }
    }

    /// <summary>Raised (on a pool thread) when a second start asks for the window.</summary>
    public event EventHandler? ShowRequested;

    /// <summary>Whether this is the first window (it holds the lock file).</summary>
    public bool IsFirst => _lock is not null;

    /// <summary>Takes the window lock of the data folder (the folder must exist).</summary>
    public static SingleInstance Acquire(string dataDirectory)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataDirectory).ToUpperInvariant())))[..16];
        var held = InstanceLock.TryAcquire(dataDirectory, InstanceLock.Window, out var reason);
        if (held is null)
        {
            Console.Error.WriteLine("TM Publisher is already open for this data folder (" + reason + ").");
        }

        return new SingleInstance(held, "TMPublisher-" + hash + "-window");
    }

    /// <summary>Asks the first window to show itself.</summary>
    public void SignalFirst()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(2000);
            client.Write("show\n"u8);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            // The first window is closing or not listening yet; nothing else to do for a second start.
            Console.Error.WriteLine("TM Publisher is already running: " + ex.Message);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        _lock?.Dispose();
        _stop.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(_name, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                var buffer = new byte[16];
                _ = await server.ReadAsync(buffer, _stop.Token).ConfigureAwait(false);
                ShowRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine("Single instance pipe error: " + ex.Message);
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }
    }
}
