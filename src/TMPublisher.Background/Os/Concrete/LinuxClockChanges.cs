using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Background.Os;

/// <summary>
/// Linux wall-clock jumps (NTP setting the time after boot on a computer without a clock battery, a manual change): a
/// <c>timerfd</c> on <c>CLOCK_REALTIME</c> armed with <c>TFD_TIMER_CANCEL_ON_SET</c>, whose blocking read fails with <c>ECANCELED</c> whenever
/// the clock is set. One background thread waits on it; there is no polling. The scheduler's timer counts monotonic time, so without this
/// a post would fire as late as the clock jumped.
/// </summary>
public sealed class LinuxClockChanges(ILogger<LinuxClockChanges> logger) : IDisposable
{
    private const int ClockRealtime = 0;
    private const int TimerCloseOnExec = 0x80000;
    private const int TimerAbsolute = 1;
    private const int TimerCancelOnSet = 2;
    private const int Canceled = 125;
    private const int Interrupted = 4;

    private volatile bool _stopped;
    private Thread? _thread;

    /// <summary>The wall clock was set.</summary>
    public event EventHandler? Changed;

    /// <summary>Starts the watch; logs and does nothing when the system does not offer it.</summary>
    public void Start()
    {
        if (!OperatingSystem.IsLinux() || _thread is not null)
        {
            return;
        }

        int fd;
        try
        {
            fd = Native.timerfd_create(ClockRealtime, TimerCloseOnExec);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            logger.LogWarning("Clock changes cannot be watched ({Type}: {Reason}); the schedule is re-read at least daily", ex.GetType().Name, ex.Message);
            return;
        }

        if (fd < 0 || !Arm(fd))
        {
            logger.LogWarning("Clock changes cannot be watched (timerfd error {Error}); the schedule is re-read at least daily", Marshal.GetLastPInvokeError());
            return;
        }

        _thread = new Thread(() => Watch(fd)) { IsBackground = true, Name = "TM Publisher clock watch" };
        _thread.Start();
        logger.LogInformation("Listening for clock changes");
    }

    /// <summary>Stops reporting (the waiting thread is a background thread and ends with the process).</summary>
    public void Dispose() => _stopped = true;

    private static bool Arm(int fd)
    {
        // Far in the future: the timer only exists to be canceled when the clock is set.
        var spec = new TimerSpec { ValueSeconds = DateTimeOffset.UtcNow.AddYears(10).ToUnixTimeSeconds() };
        return Native.timerfd_settime(fd, TimerAbsolute | TimerCancelOnSet, ref spec, IntPtr.Zero) == 0;
    }

    private void Watch(int fd)
    {
        var buffer = new byte[8];
        while (!_stopped)
        {
            var read = Native.read(fd, buffer, buffer.Length);
            var error = read < 0 ? Marshal.GetLastPInvokeError() : 0;
            if (_stopped)
            {
                return;
            }

            if (read < 0 && error == Interrupted)
            {
                continue;
            }

            if (read < 0 && error != Canceled)
            {
                logger.LogWarning("The clock watch stopped (read error {Error}); the schedule is re-read at least daily", error);
                return;
            }

            if (!Arm(fd))
            {
                logger.LogWarning("The clock watch stopped (timerfd error {Error}); the schedule is re-read at least daily", Marshal.GetLastPInvokeError());
                return;
            }

            if (error == Canceled)
            {
                logger.LogInformation("The clock was set; checking the schedule");
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary><c>struct itimerspec</c> on 64-bit Linux (x64, arm64): interval and value, each seconds + nanoseconds.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct TimerSpec
    {
        public long IntervalSeconds;
        public long IntervalNanoseconds;
        public long ValueSeconds;
        public long ValueNanoseconds;
    }

    private static class Native
    {
        [DllImport("libc.so.6", SetLastError = true)]
        public static extern int timerfd_create(int clockId, int flags);

        [DllImport("libc.so.6", SetLastError = true)]
        public static extern int timerfd_settime(int fd, int flags, ref TimerSpec newValue, IntPtr oldValue);

        [DllImport("libc.so.6", SetLastError = true)]
        public static extern nint read(int fd, byte[] buffer, nint count);
    }
}
