using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Settings;
using TMPublisher.Core.Text;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Startup;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// Compose: text, title, tags and files; targets from a group or picked by hand (never none); per-target preview of exactly what is
/// sent, with the same checks the sender runs; X cost estimate; send now, schedule or keep as a draft.
/// </summary>
public sealed partial class ComposeViewModel(IAccountStore accounts, IGroupStore groups, ITagSetStore tagSets, IPostStore posts, IPlatformDirectory platforms,
    IPostScheduler scheduler, AppSession session, FileDialogs dialogs, TimeProvider clock, Localizer text, ILogger<ComposeViewModel> logger) : PageViewModelBase
{
    private readonly Dictionary<string, string> _defaultTags = new(StringComparer.Ordinal);
    private Guid? _editing;
    private bool _refreshing;

    /// <inheritdoc />
    public override string TitleKey => "nav.compose";

    /// <inheritdoc />
    public override string Glyph => "✎";

    /// <inheritdoc />
    public override int Order => 10;

    /// <summary>Files in order.</summary>
    public ObservableCollection<MediaItem> Media { get; } = [];

    /// <summary>Groups, first "pick by hand".</summary>
    public ObservableCollection<GroupChoice> Groups { get; } = [];

    /// <summary>All accounts.</summary>
    public ObservableCollection<AccountPickViewModel> Accounts { get; } = [];

    /// <summary>Targets in sending order.</summary>
    public ObservableCollection<ComposeTargetViewModel> Targets { get; } = [];

    /// <summary>Chosen group (or "by hand").</summary>
    [ObservableProperty]
    public partial GroupChoice? SelectedGroup { get; set; }

    /// <summary>Title.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    /// <summary>Text.</summary>
    [ObservableProperty]
    public partial string Body { get; set; } = string.Empty;

    /// <summary>Tags of this post (added on every platform).</summary>
    [ObservableProperty]
    public partial string Tags { get; set; } = string.Empty;

    /// <summary>The media is AI-generated.</summary>
    [ObservableProperty]
    public partial bool Synthetic { get; set; }

    /// <summary>Kind of post (from the files).</summary>
    [ObservableProperty]
    public partial PostKind Kind { get; set; }

    /// <summary>Kind, localized.</summary>
    [ObservableProperty]
    public partial string KindText { get; set; } = string.Empty;

    /// <summary>Estimated API cost (X), empty when free.</summary>
    [ObservableProperty]
    public partial string CostText { get; set; } = string.Empty;

    /// <summary>Every target can be sent and there is at least one.</summary>
    [ObservableProperty]
    public partial bool CanSend { get; set; }

    /// <summary>Date for scheduling (local).</summary>
    [ObservableProperty]
    public partial DateTimeOffset? Date { get; set; }

    /// <summary>Time for scheduling (local).</summary>
    [ObservableProperty]
    public partial TimeSpan? Time { get; set; }

    /// <summary>Whether a stored post is being edited.</summary>
    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    /// <summary>Overridable file check (tests).</summary>
    internal Func<string, bool>? FileExists { get; set; }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        var all = await accounts.ListAsync(CancellationToken.None).ConfigureAwait(true);
        var stored = await tagSets.ListAsync(CancellationToken.None).ConfigureAwait(true);
        var groupList = await groups.ListAsync(CancellationToken.None).ConfigureAwait(true);
        _defaultTags.Clear();
        foreach (var platform in platforms.All)
        {
            _defaultTags[platform.Code] = TagLine.Join(stored.FirstOrDefault(s => s.Platform == platform.Code)?.Tags ?? StarterTagSets.For(platform.Code));
        }

        var ticked = Accounts.Where(a => a.IsChecked).Select(a => a.Account.Id).ToHashSet();
        var groupId = SelectedGroup?.Group?.Id;
        _refreshing = true;
        try
        {
            Accounts.Clear();
            foreach (var account in all)
            {
                if (platforms.Find(account.Platform) is { } platform)
                {
                    var pick = new AccountPickViewModel(account, platform, OnPickToggled);
                    pick.Set(ticked.Contains(account.Id));
                    Accounts.Add(pick);
                }
            }

            Groups.Clear();
            Groups.Add(new GroupChoice(null, text["compose.by_hand"]));
            foreach (var group in groupList)
            {
                Groups.Add(new GroupChoice(group, group.Name));
            }

            SelectedGroup = Groups.FirstOrDefault(g => g.Group?.Id == groupId) ?? Groups[0];
        }
        finally
        {
            _refreshing = false;
        }

        Date ??= clock.GetLocalNow().Date;
        Refresh();
    }

    /// <summary>Loads a stored post for editing (calendar, history).</summary>
    public async Task EditAsync(Post post)
    {
        ArgumentNullException.ThrowIfNull(post);
        await ActivateAsync().ConfigureAwait(true);
        _refreshing = true;
        try
        {
            _editing = post.Id;
            IsEditing = true;
            Title = post.Title;
            Body = post.Body;
            Tags = TagLine.Join(post.Tags);
            Synthetic = post.Targets.Any(t => t.Options.GetValueOrDefault(TargetOptionKeys.Synthetic) == "true");
            Media.Clear();
            foreach (var item in post.Media)
            {
                Media.Add(item);
            }

            SelectedGroup = Groups.FirstOrDefault(g => g.Group is not null && g.Group.Id == post.GroupId) ?? Groups[0];
            var ids = post.Targets.Select(t => t.AccountId).ToHashSet();
            foreach (var pick in Accounts)
            {
                pick.Set(ids.Contains(pick.Account.Id));
            }

            if (post.ScheduledAtUtc is { } at)
            {
                var local = TimeZoneInfo.ConvertTime(at, clock.LocalTimeZone);
                Date = local.Date;
                Time = local.TimeOfDay;
            }
        }
        finally
        {
            _refreshing = false;
        }

        Refresh();
        foreach (var target in Targets)
        {
            if (post.Targets.FirstOrDefault(t => t.AccountId == target.Account.Id) is { } stored)
            {
                target.Restore(stored);
            }
        }

        Refresh();
    }

    /// <summary>Recomputes the kind, the account filter, the targets and every preview.</summary>
    public void Refresh()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            Kind = MediaFiles.KindOf(Media);
            KindText = text["kind." + Kind.ToString().ToLowerInvariant()];
            foreach (var pick in Accounts)
            {
                var caps = pick.Platform.Capabilities;
                pick.IsSupported = caps.Kinds.Contains(Kind);
                pick.Reason = pick.IsSupported ? string.Empty : text.Format(caps.UnsupportedReasons.GetValueOrDefault(Kind, "problem.kind_unsupported"), pick.Platform.DisplayName);
                if (SelectedGroup?.Group is { } group)
                {
                    pick.Set(pick.IsSupported && group.AccountIds.Contains(pick.Account.Id));
                }
                else if (!pick.IsSupported)
                {
                    pick.Set(false);
                }
            }

            SyncTargets();
            var draft = Draft();
            foreach (var target in Targets)
            {
                target.Refresh(draft, Synthetic, FileExists);
            }

            var cost = Targets.Sum(t => t.Cost);
            CostText = cost > 0 ? text.Format("compose.cost", cost.ToString("0.###", CultureInfo.InvariantCulture)) : string.Empty;
            CanSend = Targets.Count > 0 && Targets.All(t => !t.IsBlocked);
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>Adds files.</summary>
    [RelayCommand]
    private async Task AddMediaAsync()
    {
        foreach (var path in await dialogs.OpenManyAsync("compose.add_media", "compose.media_filter", MediaFiles.Patterns, multiple: true).ConfigureAwait(true))
        {
            AddFile(path);
        }
    }

    /// <summary>Adds one file (photo or video); other files are refused with a message.</summary>
    public void AddFile(string path)
    {
        if (MediaFiles.TypeOf(path) is not { } type)
        {
            Status = text.Format("compose.not_media", Path.GetFileName(path));
            return;
        }

        try
        {
            Media.Add(new MediaItem(path, type, new FileInfo(path).Length));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read the size of an added file");
            Status = text.Format("compose.file_unreadable", Path.GetFileName(path));
            return;
        }

        Refresh();
    }

    /// <summary>Removes a file.</summary>
    [RelayCommand]
    private void RemoveMedia(MediaItem? item)
    {
        if (item is not null && Media.Remove(item))
        {
            Refresh();
        }
    }

    /// <summary>Empties the form.</summary>
    [RelayCommand]
    public void Clear()
    {
        _refreshing = true;
        try
        {
            _editing = null;
            IsEditing = false;
            Title = string.Empty;
            Body = string.Empty;
            Tags = string.Empty;
            Synthetic = false;
            Media.Clear();
            Time = null;
            Date = clock.GetLocalNow().Date;
            Targets.Clear();
            foreach (var pick in Accounts)
            {
                pick.Set(false);
            }

            SelectedGroup = Groups.FirstOrDefault();
        }
        finally
        {
            _refreshing = false;
        }

        Refresh();
    }

    /// <summary>Stores the post as a draft (not sent).</summary>
    [RelayCommand]
    public Task SaveDraftAsync() => StoreAsync(PostStatus.Draft, ChosenTime(), requireTargets: false, "compose.saved_draft");

    /// <summary>Schedules the post for the chosen date and time.</summary>
    [RelayCommand]
    public Task ScheduleAsync()
    {
        if (ChosenTime() is not { } at)
        {
            Status = text["compose.pick_time"];
            return Task.CompletedTask;
        }

        if (at <= clock.GetUtcNow())
        {
            Status = text["compose.time_past"];
            return Task.CompletedTask;
        }

        return StoreAsync(PostStatus.Scheduled, at, requireTargets: true, "compose.scheduled");
    }

    /// <summary>Sends the post now (the scheduler picks it up at once).</summary>
    [RelayCommand]
    public Task SendNowAsync() => StoreAsync(PostStatus.Scheduled, clock.GetUtcNow(), requireTargets: true, "compose.sending");

    partial void OnSelectedGroupChanged(GroupChoice? value) => Refresh();

    partial void OnTitleChanged(string value) => Refresh();

    partial void OnBodyChanged(string value) => Refresh();

    partial void OnTagsChanged(string value) => Refresh();

    partial void OnSyntheticChanged(bool value) => Refresh();

    private void OnPickToggled(AccountPickViewModel pick)
    {
        if (_refreshing)
        {
            return;
        }

        if (SelectedGroup?.Group is not null)
        {
            // A change by hand leaves the group: the targets are now picked by hand, starting from what is ticked.
            var ticked = Accounts.Where(a => a.IsChecked).ToList();
            _refreshing = true;
            SelectedGroup = Groups[0];
            _refreshing = false;
            foreach (var other in Accounts)
            {
                other.Set(ticked.Contains(other));
            }
        }

        Refresh();
    }

    private void SyncTargets()
    {
        var ticked = Accounts.Where(a => a.IsChecked).ToList();
        if (SelectedGroup?.Group is { } group)
        {
            ticked = [.. ticked.OrderBy(a => group.AccountIds.IndexOf(a.Account.Id))];
        }

        var existing = Targets.ToDictionary(t => t.Account.Id);
        Targets.Clear();
        foreach (var pick in ticked)
        {
            Targets.Add(existing.GetValueOrDefault(pick.Account.Id) ?? new ComposeTargetViewModel(pick.Account, pick.Platform,
                _defaultTags.GetValueOrDefault(pick.Platform.Code, string.Empty), !session.Settings.TikTokDraftByDefault, text, Refresh));
        }
    }

    private Post Draft() => new()
    {
        Kind = Kind,
        Title = Title.Trim(),
        Body = Body.Trim(),
        Tags = TagLine.Parse(Tags),
        Media = [.. Media],
    };

    private DateTimeOffset? ChosenTime()
    {
        if (Date is not { } date)
        {
            return null;
        }

        var local = date.Date + (Time ?? TimeSpan.Zero);
        return new DateTimeOffset(local, clock.LocalTimeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    private async Task StoreAsync(PostStatus status, DateTimeOffset? at, bool requireTargets, string doneKey)
    {
        Refresh();
        if (requireTargets && Targets.Count == 0)
        {
            Status = text["compose.no_targets"];
            return;
        }

        if (requireTargets && !CanSend)
        {
            Status = text["compose.blocked"];
            return;
        }

        if (!requireTargets && Body.Trim().Length == 0 && Title.Trim().Length == 0 && Media.Count == 0)
        {
            Status = text["compose.empty"];
            return;
        }

        var now = clock.GetUtcNow();
        var existing = _editing is { } id ? await posts.GetAsync(id, CancellationToken.None).ConfigureAwait(true) : null;
        var post = Draft();
        post.Id = existing?.Id ?? Guid.NewGuid();
        post.CreatedAtUtc = existing?.CreatedAtUtc ?? now;
        post.UpdatedAtUtc = now;
        post.Status = status;
        post.ScheduledAtUtc = at;
        post.GroupId = SelectedGroup?.Group?.Id;
        post.Targets = [.. Targets.Select((t, i) =>
        {
            var target = t.Target;
            target.PostId = post.Id;
            target.Order = i;
            target.Status = TargetStatus.Pending;
            return target;
        })];
        await posts.SaveAsync(post, CancellationToken.None).ConfigureAwait(true);
        logger.LogInformation("Post {PostId} stored as {Status} with {Targets} targets", post.Id, status, post.Targets.Count);
        scheduler.Wake();
        var when = at is { } time ? TimeZoneInfo.ConvertTime(time, clock.LocalTimeZone).ToString("g", text.Culture) : string.Empty;
        Clear();
        Status = text.Format(doneKey, when);
    }
}
