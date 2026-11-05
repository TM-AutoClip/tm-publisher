namespace TMPublisher.Storage.Backup;

/// <summary>
/// Result of a restore. Media files are not in the backup (database only): files of unsent posts that are missing on this computer are
/// listed so the user can put them back or edit the posts.
/// </summary>
public sealed record RestoreReport(int Accounts, int Posts, IReadOnlyList<string> MissingFiles);
