namespace TMPublisher.Platforms.YouTube;

/// <summary>Writes a chunk in 64 KB slices and reports progress, so a single-chunk video does not jump from 0% to done.</summary>
internal sealed class ProgressContent(byte[] buffer, int count, long before, long total, IProgress<double>? progress) : HttpContent
{
    private const int Slice = 64 * 1024;

    protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context, CancellationToken cancellationToken)
    {
        for (var written = 0; written < count;)
        {
            var n = Math.Min(Slice, count - written);
            await stream.WriteAsync(buffer.AsMemory(written, n), cancellationToken).ConfigureAwait(false);
            written += n;
            if (total > 0)
            {
                progress?.Report(Math.Clamp((before + written) / (double)total, 0, 1));
            }
        }
    }

    protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override bool TryComputeLength(out long length)
    {
        length = count;
        return true;
    }
}
