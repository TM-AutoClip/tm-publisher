using System.Net;
using System.Text;

namespace TMPublisher.Tests.Platforms;

/// <summary>
/// A scripted fake platform: every request is recorded; answers come from the first matching route (method + address part). Nothing
/// reaches the network.
/// </summary>
internal sealed class FakeHttp : HttpMessageHandler
{
    private readonly List<(HttpMethod Method, string Contains, Func<HttpRequestMessage, string, HttpResponseMessage> Answer)> _routes = [];

    public List<(HttpMethod Method, Uri Uri, string Body, HttpRequestMessage Request)> Requests { get; } = [];

    public TMPublisher.Platforms.Common.HttpTransport Transport => new(() => this);

    public FakeHttp On(HttpMethod method, string contains, Func<HttpRequestMessage, string, HttpResponseMessage> answer)
    {
        _routes.Add((method, contains, answer));
        return this;
    }

    public FakeHttp Json(HttpMethod method, string contains, string json, HttpStatusCode status = HttpStatusCode.OK) =>
        On(method, contains, (_, _) => Response(status, json));

    public static HttpResponseMessage Response(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public IEnumerable<(HttpMethod Method, Uri Uri, string Body, HttpRequestMessage Request)> To(string contains) =>
        Requests.Where(r => r.Uri.AbsoluteUri.Contains(contains, StringComparison.Ordinal));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Requests.Add((request.Method, request.RequestUri!, body, request));
        foreach (var (method, contains, answer) in _routes)
        {
            if (method == request.Method && request.RequestUri!.AbsoluteUri.Contains(contains, StringComparison.Ordinal))
            {
                return answer(request, body);
            }
        }

        return Response(HttpStatusCode.NotFound, "{\"error\":\"no fake route for " + request.Method + " " + request.RequestUri + "\"}");
    }

    protected override void Dispose(bool disposing)
    {
        // The handler is shared by the clients of a test; it holds nothing to release.
    }
}
