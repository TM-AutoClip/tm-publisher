namespace TMPublisher.Desktop.Startup;

/// <summary>Something that starts once the database is open and the main window exists (the scheduler, for example).</summary>
public interface IStartupTask
{
    /// <summary>Starts; errors are logged by the caller and shown to the user.</summary>
    Task StartAsync(CancellationToken cancellationToken);
}
