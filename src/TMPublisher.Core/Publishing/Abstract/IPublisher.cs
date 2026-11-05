namespace TMPublisher.Core.Publishing;

/// <summary>
/// Publishes to one platform. Throws <see cref="PlatformAuthException"/> (connect again), <see cref="PlatformQuotaException"/> (limit
/// reached) or <see cref="PlatformRejectedException"/> (anything else, with the platform's reason). No request is made when a local check fails.
/// </summary>
public interface IPublisher
{
    /// <summary>Publishes.</summary>
    Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken);

    /// <summary>Asks once whether a processing item finished (short follow-up after publishing).</summary>
    Task<ProcessingCheck> CheckProcessingAsync(PublishRequest request, string externalId, CancellationToken cancellationToken);
}
