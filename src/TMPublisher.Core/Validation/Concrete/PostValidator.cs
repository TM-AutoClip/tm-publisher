using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Text;

namespace TMPublisher.Core.Validation;

/// <summary>
/// RULE ENGINE: what may go to a platform. The compose screen shows the problems, the dispatcher runs the same check before any request.
/// Nothing is cut or converted silently: a too long text or too large file blocks the target with a clear reason.
/// Order: kind supported, media count and files, sizes, title, text length, hashtags.
/// </summary>
public static class PostValidator
{
    /// <summary>Problems of one target; empty when it can be sent. <paramref name="fileExists"/> is injectable for tests.</summary>
    public static IReadOnlyList<ValidationProblem> Validate(IPlatform platform, Post post, PostTarget target, Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(target);
        var exists = fileExists ?? File.Exists;
        var caps = platform.Capabilities;
        var problems = new List<ValidationProblem>();
        if (!caps.Kinds.Contains(post.Kind))
        {
            problems.Add(new ValidationProblem(caps.UnsupportedReasons.GetValueOrDefault(post.Kind, "problem.kind_unsupported"), platform.DisplayName));
            return problems;
        }

        CheckMedia(platform, post, exists, problems);
        CheckText(platform, post, target, problems);
        return problems;
    }

    private static void CheckMedia(IPlatform platform, Post post, Func<string, bool> exists, List<ValidationProblem> problems)
    {
        var caps = platform.Capabilities;
        var photos = post.Media.Count(m => m.Type == MediaType.Photo);
        var videos = post.Media.Count(m => m.Type == MediaType.Video);
        switch (post.Kind)
        {
            case PostKind.Text when post.Media.Count > 0:
                problems.Add(new ValidationProblem("problem.text_has_media"));
                break;
            case PostKind.Photo when photos == 0 || videos > 0:
                problems.Add(new ValidationProblem("problem.photo_needs_photos"));
                break;
            case PostKind.Photo when caps.MaxPhotos > 0 && photos > caps.MaxPhotos:
                problems.Add(new ValidationProblem("problem.too_many_photos", platform.DisplayName, caps.MaxPhotos));
                break;
            case PostKind.Video when videos != 1 || photos > 0:
                problems.Add(new ValidationProblem("problem.video_needs_one_video"));
                break;
            case PostKind.Album when post.Media.Count < 2:
                problems.Add(new ValidationProblem("problem.album_needs_items"));
                break;
            case PostKind.Album when caps.MaxAlbumItems > 0 && post.Media.Count > caps.MaxAlbumItems:
                problems.Add(new ValidationProblem("problem.too_many_items", platform.DisplayName, caps.MaxAlbumItems));
                break;
        }

        foreach (var media in post.Media)
        {
            var name = Path.GetFileName(media.Path);
            if (!exists(media.Path))
            {
                problems.Add(new ValidationProblem("problem.file_missing", name));
                continue;
            }

            var max = media.Type == MediaType.Photo ? caps.MaxPhotoBytes : caps.MaxVideoBytes;
            if (max > 0 && media.Bytes > max)
            {
                problems.Add(new ValidationProblem("problem.file_too_large", name, platform.DisplayName, SizeText(max)));
            }
        }
    }

    private static void CheckText(IPlatform platform, Post post, PostTarget target, List<ValidationProblem> problems)
    {
        var caps = platform.Capabilities;
        if (caps.HasTitle)
        {
            if (target.Title.Trim().Length == 0)
            {
                problems.Add(new ValidationProblem("problem.title_required", platform.DisplayName));
            }
            else if (caps.MaxTitleLength > 0 && GraphemeMeasure.Instance.Measure(target.Title) > caps.MaxTitleLength)
            {
                problems.Add(new ValidationProblem("problem.title_too_long", platform.DisplayName, caps.MaxTitleLength));
            }
        }

        if (caps.MaxTextLength == 0)
        {
            return;
        }

        var limit = post.Media.Count > 0 && caps.MaxMediaTextLength > 0 ? caps.MaxMediaTextLength : caps.MaxTextLength;
        var length = caps.TextMeasure.Measure(target.Text);
        if (length > limit)
        {
            problems.Add(new ValidationProblem("problem.text_too_long", platform.DisplayName, length, limit));
        }

        var tags = HashtagRules.TagsIn(target.Text).Count;
        if (caps.MaxHashtags > 0 && tags > caps.MaxHashtags)
        {
            problems.Add(new ValidationProblem("problem.too_many_hashtags", platform.DisplayName, tags, caps.MaxHashtags));
        }
    }

    /// <summary>Readable size ("300 MB", "3.2 MB"), invariant culture.</summary>
    public static string SizeText(long bytes)
    {
        const long MiB = 1024L * 1024, GiB = MiB * 1024;
        return bytes >= GiB
            ? (bytes / (double)GiB).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " GB"
            : (bytes / (double)MiB).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " MB";
    }
}
