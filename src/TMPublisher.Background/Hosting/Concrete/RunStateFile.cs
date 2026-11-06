using System.Text.Json;
using Microsoft.Extensions.Logging;
using TMPublisher.Background.Serialization;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Background.Hosting;

/// <summary><c>run-state.json</c>: read at the start of a run, written at its end. A missing or broken file means a fresh state.</summary>
public sealed class RunStateFile(AppPaths paths, ILogger<RunStateFile> logger)
{
    private string File => Path.Combine(paths.DataDirectory, "run-state.json");

    /// <summary>The state the last run left.</summary>
    public RunState Read()
    {
        try
        {
            return System.IO.File.Exists(File)
                ? JsonSerializer.Deserialize(System.IO.File.ReadAllText(File), BackgroundJsonContext.Default.RunState) ?? new RunState()
                : new RunState();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning("The run state could not be read ({Type}); starting fresh", ex.GetType().Name);
            return new RunState();
        }
    }

    /// <summary>Writes the state (a failure is logged, not thrown).</summary>
    public void Write(RunState state)
    {
        try
        {
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(state, BackgroundJsonContext.Default.RunState));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("The run state could not be written ({Type}): {Reason}", ex.GetType().Name, ex.Message);
        }
    }
}
