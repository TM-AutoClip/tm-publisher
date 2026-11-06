using TMPublisher.Storage.Keys;

namespace TMPublisher.Tests.Background;

/// <summary>An "automatic" protector that keeps the key as is (tests only: stands in for DPAPI / Secret Service, which tests do not touch).</summary>
internal sealed class TestKeyProtector : IKeyProtector
{
    public string Method => "test";

    public bool IsAvailable => true;

    public bool Automatic => true;

    public Task<KeyEnvelope> ProtectAsync(byte[] key, CancellationToken cancellationToken) =>
        Task.FromResult(new KeyEnvelope(Method, Convert.ToBase64String(key)));

    public Task<byte[]> UnprotectAsync(KeyEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return Task.FromResult(Convert.FromBase64String(envelope.Payload));
    }

    public Task ForgetAsync(KeyEnvelope envelope, CancellationToken cancellationToken) => Task.CompletedTask;
}
