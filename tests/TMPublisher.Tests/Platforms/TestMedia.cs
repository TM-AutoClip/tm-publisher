using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;

namespace TMPublisher.Tests.Platforms;

/// <summary>Small media files and requests for publisher tests.</summary>
internal sealed class TestMedia : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "tmpublisher-tests", "media-" + Guid.NewGuid().ToString("N"));

    public TestMedia() => Directory.CreateDirectory(_folder);

    public MediaItem Photo(string name = "photo.jpg", int bytes = 1000) => Write(name, bytes, MediaType.Photo);

    public MediaItem Video(string name = "video.mp4", int bytes = 5000) => Write(name, bytes, MediaType.Video);

    public static PublishRequest Request(Post post, string text, Credentials credentials, Func<CancellationToken, Task<Credentials>>? renew = null,
        string? handle = null, Dictionary<string, string>? options = null, string title = "")
    {
        var target = new PostTarget { Text = text, Title = title, Options = options ?? [] };
        post.Targets.Add(target);
        return new PublishRequest(new Account { Handle = handle, DisplayName = "Example" }, credentials, post, target, renew);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A stream may still be closing on Windows; the temp folder is cleaned later.
        }
    }

    private MediaItem Write(string name, int bytes, MediaType type)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, Enumerable.Range(0, bytes).Select(i => (byte)(i % 251)).ToArray());
        return new MediaItem(path, type, bytes);
    }
}
