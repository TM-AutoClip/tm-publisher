using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Text;
using TMPublisher.Core.Validation;
using TMPublisher.Desktop.Localization;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// One target in the compose screen: its own tag line and options, and the exact text, title and keywords that will be sent. The
/// <see cref="Target"/> built here is the object that is stored; publishing sends it unchanged ("what is published = what the form shows").
/// </summary>
public sealed partial class ComposeTargetViewModel : ObservableObject
{
    private readonly Localizer _text;
    private readonly Action _changed;

    /// <summary>Creates.</summary>
    public ComposeTargetViewModel(Account account, IPlatform platform, string tagLine, bool tikTokDirect, Localizer text, Action changed)
    {
        ArgumentNullException.ThrowIfNull(platform);
        Account = account;
        Platform = platform;
        _text = text;
        _changed = changed;
        PrivacyOptions = [new("public", text["privacy.public"]), new("unlisted", text["privacy.unlisted"]), new("private", text["privacy.private"])];
        Privacy = PrivacyOptions.Single(o => o.Code == (platform.Code == PlatformCodes.YouTube ? "private" : "public"));
        TagLine = tagLine;
        TikTokDirect = tikTokDirect;
    }

    /// <summary>Account.</summary>
    public Account Account { get; }

    /// <summary>Platform.</summary>
    public IPlatform Platform { get; }

    /// <summary>"Platform · name".</summary>
    public string Title => Platform.DisplayName + " · " + Account.DisplayName;

    /// <summary>Platform color.</summary>
    public string Color => Platform.Color;

    /// <summary>The target as it will be stored and sent.</summary>
    public PostTarget Target { get; private set; } = new();

    /// <summary>Visibility choices.</summary>
    public IReadOnlyList<ChoiceOption> PrivacyOptions { get; }

    /// <summary>The platform has a visibility option.</summary>
    public bool ShowPrivacy => Platform.Capabilities.SupportsPrivacy;

    /// <summary>TikTok.</summary>
    public bool IsTikTok => Platform.Code == PlatformCodes.TikTok;

    /// <summary>YouTube.</summary>
    public bool IsYouTube => Platform.Code == PlatformCodes.YouTube;

    /// <summary>Pinterest.</summary>
    public bool IsPinterest => Platform.Code == PlatformCodes.Pinterest;

    /// <summary>Platform has a title field.</summary>
    public bool HasTitle => Platform.Capabilities.HasTitle;

    /// <summary>This platform's tags for this post (filled from the platform's default set).</summary>
    [ObservableProperty]
    public partial string TagLine { get; set; } = string.Empty;

    /// <summary>Visibility.</summary>
    [ObservableProperty]
    public partial ChoiceOption? Privacy { get; set; }

    /// <summary>TikTok: direct private post instead of an inbox draft.</summary>
    [ObservableProperty]
    public partial bool TikTokDirect { get; set; }

    /// <summary>YouTube: made for kids.</summary>
    [ObservableProperty]
    public partial bool MadeForKids { get; set; }

    /// <summary>Pinterest: destination link.</summary>
    [ObservableProperty]
    public partial string PinterestLink { get; set; } = string.Empty;

    /// <summary>The text that will be sent.</summary>
    [ObservableProperty]
    public partial string Preview { get; set; } = string.Empty;

    /// <summary>The title that will be sent (platforms with a title field).</summary>
    [ObservableProperty]
    public partial string TitlePreview { get; set; } = string.Empty;

    /// <summary>Keywords (YouTube tags field).</summary>
    [ObservableProperty]
    public partial string KeywordsText { get; set; } = string.Empty;

    /// <summary>"used / limit" of the text.</summary>
    [ObservableProperty]
    public partial string Counter { get; set; } = string.Empty;

    /// <summary>Problems (localized, one per line); empty when it can be sent.</summary>
    [ObservableProperty]
    public partial string ProblemsText { get; set; } = string.Empty;

    /// <summary>A note about how this platform handles the post (TikTok draft...).</summary>
    [ObservableProperty]
    public partial string Note { get; set; } = string.Empty;

    /// <summary>Cannot be sent as it is.</summary>
    [ObservableProperty]
    public partial bool IsBlocked { get; set; }

    /// <summary>Estimated cost of this target in US dollars.</summary>
    public decimal Cost { get; private set; }

    /// <summary>Puts stored options back (editing a post).</summary>
    public void Restore(PostTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        TagLine = target.Options.GetValueOrDefault(TargetOptionKeys.Tags, TagLine);
        Privacy = PrivacyOptions.FirstOrDefault(o => o.Code == target.Options.GetValueOrDefault(TargetOptionKeys.Privacy)) ?? Privacy;
        TikTokDirect = target.Options.GetValueOrDefault(TargetOptionKeys.TikTokMode) == "direct";
        MadeForKids = target.Options.GetValueOrDefault(TargetOptionKeys.MadeForKids) == "true";
        PinterestLink = target.Options.GetValueOrDefault(TargetOptionKeys.PinterestLink, string.Empty);
    }

    /// <summary>Composes the target from the form and checks it with the same rules the sender uses.</summary>
    public void Refresh(Post draft, bool synthetic, Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var caps = Platform.Capabilities;
        var (text, title, keywords) = PostComposer.Compose(caps, draft.Title, draft.Body, Core.Text.TagLine.Parse(TagLine), draft.Tags);
        Target = new PostTarget { AccountId = Account.Id, Text = text, Title = title, Keywords = keywords, Options = Options(synthetic) };
        Preview = text;
        TitlePreview = title;
        KeywordsText = string.Join(", ", keywords);
        var limit = draft.Media.Count > 0 && caps.MaxMediaTextLength > 0 ? caps.MaxMediaTextLength : caps.MaxTextLength;
        Counter = limit > 0 ? caps.TextMeasure.Measure(text).ToString(CultureInfo.InvariantCulture) + " / " + limit.ToString(CultureInfo.InvariantCulture) : string.Empty;
        var problems = PostValidator.Validate(Platform, draft, Target, fileExists);
        ProblemsText = string.Join(Environment.NewLine, problems.Select(p => _text.Format(p.Key, p.Args)));
        IsBlocked = problems.Count > 0;
        Cost = CostEstimator.Estimate(caps, text);
        Note = IsTikTok ? _text[TikTokDirect ? "compose.tiktok_direct_note" : "compose.tiktok_draft_note"] : string.Empty;
    }

    partial void OnTagLineChanged(string value) => _changed();

    partial void OnPrivacyChanged(ChoiceOption? value) => _changed();

    partial void OnTikTokDirectChanged(bool value) => _changed();

    partial void OnMadeForKidsChanged(bool value) => _changed();

    partial void OnPinterestLinkChanged(string value) => _changed();

    private Dictionary<string, string> Options(bool synthetic)
    {
        var options = new Dictionary<string, string>
        {
            [TargetOptionKeys.Tags] = TagLine.Trim(),
            [TargetOptionKeys.Synthetic] = synthetic ? "true" : "false",
        };
        if (ShowPrivacy && Privacy is not null)
        {
            options[TargetOptionKeys.Privacy] = Privacy.Code;
        }

        if (IsTikTok)
        {
            options[TargetOptionKeys.TikTokMode] = TikTokDirect ? "direct" : "draft";
        }

        if (IsYouTube)
        {
            options[TargetOptionKeys.MadeForKids] = MadeForKids ? "true" : "false";
        }

        if (IsPinterest && PinterestLink.Trim().Length > 0)
        {
            options[TargetOptionKeys.PinterestLink] = PinterestLink.Trim();
        }

        return options;
    }
}
