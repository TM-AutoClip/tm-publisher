namespace TMPublisher.Core.Connections;

/// <summary>What a connection method may ask of the connection window while it runs.</summary>
public interface IConnectionPrompt
{
    /// <summary>Opens the address in the user's browser.</summary>
    Task OpenBrowserAsync(Uri address, CancellationToken cancellationToken);

    /// <summary>Shows a paste box with the prompt (localization key) and waits for the user's value.</summary>
    Task<string> AskPasteAsync(string promptKey, CancellationToken cancellationToken);

    /// <summary>Shows a status line (localization key).</summary>
    void Status(string messageKey);
}
