namespace TMPublisher.Desktop.Paths;

/// <summary>Plain preferences needed before unlocking (the language of the unlock window, the update check). Nothing secret.</summary>
public sealed class UiPreferences
{
    /// <summary>Interface language (<c>en</c> / <c>tr</c>); empty until chosen.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Look for a new version at every start of the window (on by default; Settings → Updates).</summary>
    public bool CheckForUpdates { get; set; } = true;
}
