namespace TMPublisher.Desktop.Os;

/// <summary>Shows a file in the system's file manager (Windows: Explorer with the file selected; Linux: its folder).</summary>
public interface IFileReveal
{
    /// <summary>Shows <paramref name="file"/>; an exception when no file manager could be started.</summary>
    void Reveal(string file);
}
