using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shipping.Contracts;

namespace Shipping.ConsoleClient;

internal static class ExitCodes
{
    public const int Success = 0;
    public const int Failure = 1;
}

/// <summary>
/// Lists the catalogue and asks the API to quote a package. Writes to injected
/// TextWriters and talks over an injected HttpClient, so the behaviour can be
/// exercised without a console or a network.
/// </summary>
internal sealed class ShippingConsoleClient
{
    // The API serialises with ASP.NET's web defaults (camelCase), so read it back the same way.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public ShippingConsoleClient(HttpClient http, TextWriter output, TextWriter error)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    /// <summary>The API address as typed, without the trailing slash a Uri normalises in.</summary>
    private string Address => _http.BaseAddress?.ToString().TrimEnd('/') ?? "the API";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = QuoteArguments.Parse(args);

            await ShowPackagesAsync(cancellationToken).ConfigureAwait(false);
            await ShowQuoteAsync(request, cancellationToken).ConfigureAwait(false);

            return ExitCodes.Success;
        }
        catch (FormatException ex)
        {
            _error.WriteLine(ex.Message);
            _error.WriteLine(QuoteArguments.Usage);
            return ExitCodes.Failure;
        }
        catch (HttpRequestException ex)
        {
            _error.WriteLine($"Could not reach the API at {Address}: {ex.Message}");
            _error.WriteLine("Start it with: dotnet run --project src/Shipping.Api");
            return ExitCodes.Failure;
        }
    }

    private async Task ShowPackagesAsync(CancellationToken cancellationToken)
    {
        var packageTypes = await _http
            .GetFromJsonAsync<IReadOnlyList<PackageTypeResponse>>("/api/packages", Json, cancellationToken)
            .ConfigureAwait(false)
            ?? [];

        _output.WriteLine($"Package types at {Address}");
        _output.WriteLine();

        foreach (var packageType in packageTypes)
        {
            var size = packageType.Dimensions;

            _output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {packageType.Name,-8} " +
                $"{size.LengthMm,4} x {size.BreadthMm,4} x {size.HeightMm,4} mm   " +
                $"{packageType.Cost,6:0.00} NZD"));
        }

        _output.WriteLine();
    }

    private async Task ShowQuoteAsync(QuoteRequest request, CancellationToken cancellationToken)
    {
        _output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Quoting {request.LengthMm}x{request.BreadthMm}x{request.HeightMm}mm " +
            $"at {request.WeightKg}kg"));
        _output.WriteLine();

        using var response = await _http
            .PostAsJsonAsync("/api/quotes", request, Json, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            await WriteQuoteAsync(response, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteFailureAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteQuoteAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var quote = await response.Content
            .ReadFromJsonAsync<QuoteResponse>(Json, cancellationToken)
            .ConfigureAwait(false);

        if (quote is null)
        {
            _error.WriteLine("The API returned an empty quote.");
            return;
        }

        _output.WriteLine($"  Package type : {quote.PackageType}");
        _output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  Cost         : {quote.Cost:0.00} NZD"));
    }

    private async Task WriteFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var error = await response.Content
            .ReadFromJsonAsync<ApiErrorResponse>(Json, cancellationToken)
            .ConfigureAwait(false);

        var detail = error?.Detail ?? response.ReasonPhrase ?? "The request failed.";

        // 422 means the request was fine but the package cannot be shipped - the most
        // interesting failure this API has, so name the reason rather than the status.
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            _output.WriteLine($"  No packaging solution ({error?.Reason}): {detail}");
            return;
        }

        _error.WriteLine($"  {(int)response.StatusCode} {error?.Title}: {detail}");

        foreach (var message in error?.Errors ?? [])
        {
            _error.WriteLine($"    - {message}");
        }
    }
}
