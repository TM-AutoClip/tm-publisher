namespace TMPublisher.Desktop.ViewModels;

/// <summary>Page of the first-run wizard.</summary>
public enum FirstRunStep
{
    /// <summary>Choose the language.</summary>
    Language = 0,

    /// <summary>New start or restore.</summary>
    Choice = 1,

    /// <summary>New start: protection and autostart.</summary>
    New = 2,

    /// <summary>Restore from a backup file.</summary>
    Restore = 3,
}
