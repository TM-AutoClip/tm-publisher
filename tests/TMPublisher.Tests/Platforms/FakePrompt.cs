using TMPublisher.Core.Connections;

namespace TMPublisher.Tests.Platforms;

/// <summary>A connection window stand-in: records opened addresses and answers paste requests.</summary>
internal sealed class FakePrompt(Func<Uri?, string>? paste = null) : IConnectionPrompt
{
    public List<Uri> Opened { get; } = [];

    public List<string> Statuses { get; } = [];

    public Task OpenBrowserAsync(Uri address, CancellationToken cancellationToken)
    {
        Opened.Add(address);
        return Task.CompletedTask;
    }

    public Task<string> AskPasteAsync(string promptKey, CancellationToken cancellationToken) =>
        Task.FromResult(paste?.Invoke(Opened.LastOrDefault()) ?? string.Empty);

    public void Status(string messageKey) => Statuses.Add(messageKey);
}
