using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;

namespace TMPublisher.Tests.Scheduling;

/// <summary>A platform whose publisher follows a script and records what it was asked to send.</summary>
internal sealed class ScriptedPlatform(string code = "scripted", int? dailyLimit = null) : IPlatform, IPublisher
{
    public List<PostTarget> Sent { get; } = [];

    public Queue<Func<PublishRequest, PublishResult>> Script { get; } = new();

    public Func<string, ProcessingCheck> Processing { get; set; } = _ => new ProcessingCheck(TargetStatus.Processing);

    public string Code => code;

    public string DisplayName => "Scripted";

    public string Color => "#000000";

    public PlatformCapabilities Capabilities { get; } = new()
    {
        Kinds = new HashSet<PostKind> { PostKind.Text },
        MaxTextLength = 500,
        TextMeasure = GraphemeMeasure.Instance,
        Quotas = dailyLimit is { } max ? [new QuotaRule(max, QuotaWindow.Rolling24Hours)] : [],
    };

    public IConnectionMethod Connection => throw new NotSupportedException();

    public SetupGuide Guide => new([], []);

    public IPublisher Publisher => this;

    public ITokenRefresher? Refresher => null;

    public Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        Sent.Add(request.Target);
        var step = Script.Count > 0 ? Script.Dequeue() : _ => new PublishResult("id-" + Sent.Count, "https://example.com/" + Sent.Count, TargetStatus.Published);
        return Task.FromResult(step(request));
    }

    public Task<ProcessingCheck> CheckProcessingAsync(PublishRequest request, string externalId, CancellationToken cancellationToken) =>
        Task.FromResult(Processing(externalId));
}
