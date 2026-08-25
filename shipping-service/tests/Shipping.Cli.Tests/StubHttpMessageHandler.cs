using System.Net;
using System.Text;

namespace Shipping.Cli.Tests;

/// <summary>
/// Serves canned responses so the CLI's behaviour can be tested without a running
/// API, and records what was sent so request bodies can be asserted on.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _respond;

    public StubHttpMessageHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond)
        => _respond = respond;

    public List<(HttpMethod Method, string Path, string Body)> Requests { get; } = [];

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));

        return _respond(request, body);
    }
}
