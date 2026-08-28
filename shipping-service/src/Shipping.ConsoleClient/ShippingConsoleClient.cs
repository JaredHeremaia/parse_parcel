using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shipping.Contracts;

namespace Shipping.ConsoleClient;

internal static class ExitCodes
{
    public const int Success = 0;

    /// <summary>Bad usage, or the API could not be reached at all.</summary>
    public const int Failure = 1;

    /// <summary>The API was reached and refused the request.</summary>
    public const int ApiError = 2;
}

/// <summary>
/// Drives the shipping API from the command line. Writes to injected TextWriters and talks
/// over an injected HttpClient, so the behaviour can be exercised without a console or a
/// network.
/// </summary>
internal sealed class ShippingConsoleClient
{
    // The API serialises with ASP.NET's web defaults (camelCase), so read it back the same way.
    // Nulls are dropped on the way out so a patch carries only the fields actually being
    // changed, rather than a body full of nulls the server would have to read as "absent".
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

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
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            if (args.Length == 0)
            {
                await ShowPackagesAsync(cancellationToken).ConfigureAwait(false);
                await ShowQuoteAsync(CommandLine.Sample, cancellationToken).ConfigureAwait(false);
                return ExitCodes.Success;
            }

            return await DispatchAsync(args, cancellationToken).ConfigureAwait(false);
        }
        catch (FormatException ex)
        {
            _error.WriteLine(ex.Message);
            _error.WriteLine();
            _error.WriteLine(CommandLine.Usage);
            return ExitCodes.Failure;
        }
        catch (HttpRequestException ex)
        {
            _error.WriteLine($"Could not reach the API at {Address}: {ex.Message}");
            _error.WriteLine("Start it with: dotnet run --project src/Shipping.Api");
            return ExitCodes.Failure;
        }
    }

    private async Task<int> DispatchAsync(string[] args, CancellationToken cancellationToken)
    {
        switch (args[0].ToLowerInvariant())
        {
            case "help":
            case "--help":
            case "-h":
                _output.WriteLine(CommandLine.Usage);
                return ExitCodes.Success;

            case "list":
                await ShowPackagesAsync(cancellationToken).ConfigureAwait(false);
                return ExitCodes.Success;

            case "quote":
                await ShowQuoteAsync(CommandLine.ParseQuote(args), cancellationToken).ConfigureAwait(false);
                return ExitCodes.Success;

            case "add":
                return await AddAsync(CommandLine.ParseAdd(args), cancellationToken).ConfigureAwait(false);

            case "update":
                var (id, request) = CommandLine.ParseUpdate(args);
                return await UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);

            case "delete":
                return await DeleteAsync(CommandLine.ParseDelete(args), cancellationToken).ConfigureAwait(false);

            default:
                throw new FormatException($"Unknown command '{args[0]}'.");
        }
    }

    private async Task<int> AddAsync(PackageTypeRequest request, CancellationToken cancellationToken)
    {
        using var response = await _http
            .PostAsJsonAsync("/api/packages", request, Json, cancellationToken)
            .ConfigureAwait(false);

        return await WriteChangedPackageAsync(response, "Added", cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> UpdateAsync(
        Guid id,
        PackageTypePatchRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _http
            .PatchAsJsonAsync($"/api/packages/{id}", request, Json, cancellationToken)
            .ConfigureAwait(false);

        return await WriteChangedPackageAsync(response, "Updated", cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await _http
            .DeleteAsync($"/api/packages/{id}", cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await WriteFailureAsync(response, cancellationToken).ConfigureAwait(false);
            return ExitCodes.ApiError;
        }

        // A successful delete is 204 with no body, so there is nothing to read back.
        _output.WriteLine($"Deleted {id}.");
        return ExitCodes.Success;
    }

    /// <summary>Shared tail of add and update: report the package the API returned, or the failure.</summary>
    private async Task<int> WriteChangedPackageAsync(
        HttpResponseMessage response,
        string verb,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            await WriteFailureAsync(response, cancellationToken).ConfigureAwait(false);
            return ExitCodes.ApiError;
        }

        var packageType = await response.Content
            .ReadFromJsonAsync<PackageTypeResponse>(Json, cancellationToken)
            .ConfigureAwait(false);

        if (packageType is null)
        {
            _error.WriteLine("The API returned an empty response.");
            return ExitCodes.ApiError;
        }

        _output.WriteLine($"{verb} '{packageType.Name}'.");
        _output.WriteLine(Line(packageType));
        _output.WriteLine($"  {packageType.Id}");
        return ExitCodes.Success;
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
            _output.WriteLine(Line(packageType));
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

    private static string Line(PackageTypeResponse packageType)
    {
        var size = packageType.Dimensions;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"  {packageType.Name,-12} " +
            $"{size.LengthMm,4} x {size.BreadthMm,4} x {size.HeightMm,4} mm   " +
            $"{packageType.Cost,6:0.00} NZD");
    }
}
