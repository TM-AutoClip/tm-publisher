using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Tests.Platforms;

/// <summary>A pasted-token platform on the fake network: GET /me returns the identity of the pasted token.</summary>
internal sealed class FakePlatform(FakeHttp http) : PlatformBase
{
    public override string Code => "fake";

    public override string DisplayName => "Fake";

    public override string Color => "#123456";

    public override PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Text },
        MaxTextLength = 100,
        TextMeasure = GraphemeMeasure.Instance,
    };

    public override IConnectionMethod Connection { get; } = new FakeConnection(http.Transport);

    public override SetupGuide Guide => new([new GuideStep("guide.fake.1", new Uri("https://example.com/dev"), CredentialKeys.AccessToken)], ["warning.fake"]);

    public override IPublisher Publisher => throw new NotSupportedException();

    /// <summary>The fake connection.</summary>
    private sealed class FakeConnection(HttpTransport transport) : PastedTokenConnectionBase(transport, TimeProvider.System)
    {
        public override IReadOnlyList<ConnectionField> Fields => [new(CredentialKeys.AccessToken, "field.access_token", Secret: true)];

        protected override Task<Credentials> ConvertAsync(ConnectionInput input, CancellationToken cancellationToken) =>
            Task.FromResult(FromInput(input, CredentialKeys.AccessToken));

        protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
        {
            var me = await Http.GetJsonAsync("fake profile", "https://fake.example/me", credentials.Require(CredentialKeys.AccessToken), cancellationToken);
            return new AccountIdentity(JsonSafe.Str(me, "id"), JsonSafe.Str(me, "name"), "@" + JsonSafe.Str(me, "name"));
        }
    }
}
