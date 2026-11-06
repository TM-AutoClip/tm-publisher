using Microsoft.Extensions.Logging;
using TMPublisher.Runtime.Os;

namespace TMPublisher.Background.Legacy;

/// <summary>
/// What 0.1.0–0.1.4 left that would still start an old program: their autostart entries of this data folder are removed at every start of the
/// window and of a run (one look at a registry value or a file; logged only when something was removed). A 0.1.3–0.1.4 scheduler that still
/// runs is ended by <see cref="Sending.SenderGate"/>. Its program file is only mentioned (the window logs it once): it is not used any more.
/// </summary>
public sealed class LegacyCleanup(IEnumerable<ILegacyAutostart> entries, ILogger<LegacyCleanup> logger)
{
    /// <summary>Removes the leftovers; errors are logged (an old entry must not stop sending).</summary>
    public void Run(bool mentionOldProgram)
    {
        foreach (var entry in entries)
        {
            try
            {
                entry.RemoveLeftover();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                logger.LogError(ex, "An autostart entry of TM Publisher 0.1.0-0.1.4 could not be removed; it may still start an old program at sign-in");
            }
        }

        if (mentionOldProgram && ProgramFiles.OldScheduler is not null)
        {
            logger.LogInformation("The scheduler program of 0.1.3-0.1.4 is still next to TM Publisher; it is not used any more and can be deleted");
        }
    }
}
