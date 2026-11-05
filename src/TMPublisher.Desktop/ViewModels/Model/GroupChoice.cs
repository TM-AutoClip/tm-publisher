using TMPublisher.Core.Accounts;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>A group in the compose screen's list; <see cref="Group"/> is <c>null</c> for "pick accounts by hand".</summary>
public sealed record GroupChoice(Group? Group, string Name);
