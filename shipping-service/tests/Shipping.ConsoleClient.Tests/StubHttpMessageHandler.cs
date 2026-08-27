using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Shipping.ConsoleClient.Tests;

/// <summary>
/// Answers requests from a canned function instead of the network, and records what
/// was asked so a test can assert on the request as well as the output.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => _respond = respond;

    /// <summary>Requests the client made, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Bodies of those requests, captured before the content is disposed.</summary>
    public List<string> Bodies { get; } = [];

    /// <summary>A handler that fails as though nothing is listening.</summary>
    public static StubHttpMessageHandler Unreachable()
        => new(_ => throw new HttpRequestException("Connection refused (localhost:5080)"));

    public static HttpResponseMessage Ok<T>(T value)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(value, options: Json) };

    /// <summary>An RFC 7807 problem document, as the API returns for a failure.</summary>
    public static HttpResponseMessage Problem<T>(HttpStatusCode status, T value)
        => new(status)
        {
            Content = JsonContent.Create(
                value,
                mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("application/problem+json"),
                options: Json),
        };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        Bodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

        return _respond(request);
    }
}
