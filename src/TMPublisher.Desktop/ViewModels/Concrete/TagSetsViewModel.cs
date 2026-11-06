using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Settings;
using TMPublisher.Core.Text;
using TMPublisher.Desktop.Localization;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>Default tags per platform: they fill the compose form (each post can still change them).</summary>
public sealed partial class TagSetsViewModel(ITagSetStore store, IPlatformDirectory platforms, Localizer text, ILogger<TagSetsViewModel> logger)
    : PageViewModelBase, IPageInfo
{
    /// <inheritdoc />
    public static string TitleKey => "nav.tags";

    /// <inheritdoc />
    public static string Glyph => "#";

    /// <inheritdoc />
    public static int Order => 60;

    /// <summary>One row per platform.</summary>
    public ObservableCollection<TagSetRowViewModel> Rows { get; } = [];

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        var stored = await store.ListAsync(CancellationToken.None).ConfigureAwait(true);
        Rows.Clear();
        foreach (var platform in platforms.All)
        {
            var tags = stored.FirstOrDefault(s => s.Platform == platform.Code)?.Tags ?? StarterTagSets.For(platform.Code);
            Rows.Add(new TagSetRowViewModel(platform, TagLine.Join(tags)));
        }
    }

    /// <summary>Stores every row.</summary>
    [RelayCommand]
    public async Task SaveAsync()
    {
        try
        {
            foreach (var row in Rows)
            {
                await store.SaveAsync(new TagSet { Platform = row.Platform.Code, Tags = TagLine.Parse(row.Line) }, CancellationToken.None).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Default tags could not be saved");
            Status = text.Format("error.generic", ex.Message);
            return;
        }

        logger.LogInformation("Default tags saved for {Count} platforms", Rows.Count);
        Status = text["tags.saved"];
    }
}
