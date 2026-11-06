using Microsoft.Extensions.Logging;

namespace TMPublisher.Runtime.Logging;

/// <summary>
/// Exceptions nothing caught, written as Critical with their cause: <see cref="AppDomain.UnhandledException"/> (the process ends after it) and
/// <see cref="TaskScheduler.UnobservedTaskException"/> (a faulted task nobody awaited; the process keeps running, the error must not be lost).
/// The window program also reports Avalonia's dispatcher exceptions through the same <see cref="Unhandled"/>.
/// </summary>
public static class CrashLogging
{
    /// <summary>Subscribes both handlers for the life of the process.</summary>
    public static void Attach(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Unhandled(logger, e.ExceptionObject as Exception, e.IsTerminating ? "process" : "thread");
        TaskScheduler.UnobservedTaskException += (_, e) => Unobserved(logger, e.Exception);
    }

    /// <summary>An exception that left a thread or the process (<paramref name="where"/>).</summary>
    public static void Unhandled(ILogger logger, Exception? exception, string where)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.LogCritical(exception, "Unhandled exception ({Where}, {Type})", where, exception?.GetType().Name ?? "unknown");
    }

    /// <summary>A faulted task whose exception was never observed.</summary>
    public static void Unobserved(ILogger logger, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.LogCritical(exception, "Unobserved task exception ({Type})", exception?.GetType().Name ?? "unknown");
    }
}
