using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shipping.Contracts;

namespace Shipping.Cli;

/// <summary>Raised when the API reports a problem we should show the user.</summary>
internal sealed class ApiException : Exception
{
    public ApiException(string message, HttpStatusCode? statusCode = null)
        : base(message) => StatusCode = statusCode;

    public HttpStatusCode? StatusCode { get; }
}

/// <summary>A quote attempt: either a solution, or an explained rejection.</summary>
internal sealed record QuoteOutcome(QuoteResponse? Quote, string? RejectionMessage)
{
    public bool IsQuoted => Quote is not null;
}

/// <summary>Thin typed wrapper over the HTTP API, sharing the contracts the API publishes.</summary>
internal sealed class ShippingApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public ShippingApiClient(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public Task<IReadOnlyList<PackageTypeResponse>> ListAsync(CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<PackageTypeResponse>>(
            new HttpRequestMessage(HttpMethod.Get, "api/packages"),
            cancellationToken);

    public Task<PackageTypeResponse> GetAsync(string idOrName, CancellationToken cancellationToken = default)
        => SendAsync<PackageTypeResponse>(
            new HttpRequestMessage(HttpMethod.Get, $"api/packages/{Uri.EscapeDataString(idOrName)}"),
            cancellationToken);

    public Task<PackageTypeResponse> CreateAsync(
        PackageTypeRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<PackageTypeResponse>(
            new HttpRequestMessage(HttpMethod.Post, "api/packages")
            {
                Content = JsonContent.Create(request, options: JsonOptions),
            },
            cancellationToken);

    public Task<PackageTypeResponse> UpdateAsync(
        Guid id,
        PackageTypeRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<PackageTypeResponse>(
            new HttpRequestMessage(HttpMethod.Put, $"api/packages/{id}")
            {
                Content = JsonContent.Create(request, options: JsonOptions),
            },
            cancellationToken);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(
            new HttpRequestMessage(HttpMethod.Delete, $"api/packages/{id}"),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await ToApiExceptionAsync(response, cancellationToken);
        }
    }

    /// <summary>
    /// A 422 here is not a failure of the request: it means we cannot package the
    /// item, which the caller reports as advice rather than as an error.
    /// </summary>
    public async Task<QuoteOutcome> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(
            new HttpRequestMessage(HttpMethod.Post, "api/quotes")
            {
                Content = JsonContent.Create(request, options: JsonOptions),
            },
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var problem = await ReadProblemAsync(response, cancellationToken);
            return new QuoteOutcome(null, problem?.Detail ?? "No packaging solution is available.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await ToApiExceptionAsync(response, cancellationToken);
        }

        var quote = await response.Content.ReadFromJsonAsync<QuoteResponse>(JsonOptions, cancellationToken);

        return quote is null
            ? throw new ApiException("The API returned an empty quote.")
            : new QuoteOutcome(quote, null);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await ToApiExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new ApiException("The API returned an empty response.");
    }

    private async Task<HttpResponseMessage> SendRawAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(
                $"Could not reach the shipping API at {_http.BaseAddress}. Is it running? ({ex.Message})");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiException($"The shipping API at {_http.BaseAddress} did not respond in time.");
        }
        finally
        {
            request.Dispose();
        }
    }

    private static async Task<ApiException> ToApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var problem = await ReadProblemAsync(response, cancellationToken);

        var detail = problem?.Detail
            ?? problem?.Title
            ?? $"The API returned {(int)response.StatusCode} {response.ReasonPhrase}.";

        return new ApiException(detail, response.StatusCode);
    }

    private static async Task<ApiErrorResponse?> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            // Response was not JSON (e.g. an HTML error page from a proxy).
            return null;
        }
    }
}
