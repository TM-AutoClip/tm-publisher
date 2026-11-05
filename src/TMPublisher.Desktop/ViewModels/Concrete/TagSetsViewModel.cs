using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Settings;
using TMPublisher.Core.Text;
using TMPublisher.Desktop.Localization;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>Default tags per platform: they fill the compose form (each post can still change them).</summary>
public sealed partial class TagSetsViewModel(ITagSetStore store, IPlatformDirectory platforms, Localizer text) : PageViewModelBase
{
    /// <inheritdoc />
    public override string TitleKey => "nav.tags";

    /// <inheritdoc />
    public override string Glyph => "#";

    /// <inheritdoc />
    public override int Order => 60;

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
        foreach (var row in Rows)
        {
            await store.SaveAsync(new TagSet { Platform = row.Platform.Code, Tags = TagLine.Parse(row.Line) }, CancellationToken.None).ConfigureAwait(true);
        }

        Status = text["tags.saved"];
    }
}
