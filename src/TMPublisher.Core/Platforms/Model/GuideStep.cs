namespace TMPublisher.Core.Platforms;

/// <summary>
/// One step of a platform's setup guide. <paramref name="TextKey"/> is a localization key; <paramref name="Link"/> opens a page;
/// <paramref name="CopyFieldKey"/> names a connection field whose current value the user can copy (for example the redirect address).
/// </summary>
public sealed record GuideStep(string TextKey, Uri? Link = null, string? CopyFieldKey = null);
