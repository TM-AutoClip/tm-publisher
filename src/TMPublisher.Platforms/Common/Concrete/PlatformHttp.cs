using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using TMPublisher.Core.Publishing;

namespace TMPublisher.Platforms.Common;

/// <summary>Small HTTP helpers shared by connections and publishers: send and read, JSON and form posts with a clear error.</summary>
public sealed class PlatformHttp(HttpClient client)
{
    /// <summary>The client.</summary>
    public HttpClient Client => client;

    /// <summary>Sends and reads the answer.</summary>
    public async Task<PlatformReply> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in response.Headers)
        {
            headers.TryAdd(name, values.FirstOrDefault() ?? string.Empty);
        }

        return new PlatformReply((int)response.StatusCode, body, response.Headers.RetryAfter?.Delta, headers);
    }

    /// <summary>GET with an optional bearer token; JSON body or <see cref="PlatformRejectedException"/> / <see cref="PlatformAuthException"/>.</summary>
    public async Task<JsonNode?> GetJsonAsync(string what, string address, string? bearer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return Checked(what, await SendAsync(request, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Form POST (OAuth token endpoints); JSON body or an exception.</summary>
    public async Task<JsonNode?> PostFormAsync(string what, string address, IEnumerable<KeyValuePair<string, string>> form,
        AuthenticationHeaderValue? authorization, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, address) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Authorization = authorization;
        return Checked(what, await SendAsync(request, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>A JSON request body (<c>application/json; charset=utf-8</c>), written without reflection.</summary>
    public static StringContent JsonBody(JsonNode body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
    }

    /// <summary>JSON POST with an optional bearer token; JSON body or an exception.</summary>
    public async Task<JsonNode?> PostJsonAsync(string what, string address, JsonNode body, string? bearer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        using var request = new HttpRequestMessage(HttpMethod.Post, address)
        {
            Content = JsonBody(body),
        };
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return Checked(what, await SendAsync(request, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Basic authorization header (client id + secret).</summary>
    public static AuthenticationHeaderValue Basic(string user, string password) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password)));

    private static JsonNode? Checked(string what, PlatformReply reply)
    {
        if (reply.IsSuccess)
        {
            return JsonSafe.TryParse(reply.Body);
        }

        var message = $"{what} failed (HTTP {reply.Status}): {PlatformText.Short(reply.Body)}";
        throw reply.Status == 401 ? new PlatformAuthException(message) : new PlatformRejectedException(message);
    }
}
