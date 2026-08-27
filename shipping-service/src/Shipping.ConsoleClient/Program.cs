using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shipping.Contracts;

// A small demo client for the shipping API. It lists the catalogue, then asks for a
// quote - the sample package by default, or one given on the command line:
//
//   dotnet run --project src/Shipping.ConsoleClient
//   dotnet run --project src/Shipping.ConsoleClient 200 300 150 5
//
// The API address comes from SHIPPING_API_URL, defaulting to http://localhost:5080.
// It shares the wire contracts with the API via Shipping.Contracts, so the JSON shape
// cannot drift between the two.

const string DefaultApiUrl = "http://localhost:5080";

// A raw string literal keeps the backslashes and backticks intact without escaping.
const string Banner = """
     _____              _        __  __
    |_   _| __ __ _  __| | ___  |  \/  | ___    __ _
      | || '__/ _` |/ _` |/ _ \ | |\/| |/ _ \  /  ('>--
      | || | | (_| | (_| |  __/ | |  | |  __/  \__/
      |_||_|  \__,_|\__,_|\___| |_|  |_|\___|   L\_
       W h e r e     k i w i     l o o k     f i r s t
    """;

// Decoration only, so keep it out of a pipe or a redirect to a file.
if (!Console.IsOutputRedirected)
{
    Console.WriteLine(Banner);
    Console.WriteLine();
}

var baseUrl = Environment.GetEnvironmentVariable("SHIPPING_API_URL") ?? DefaultApiUrl;

// The API serialises with ASP.NET's web defaults (camelCase), so read it back the same way.
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

try
{
    var request = ParseQuoteRequest(args);

    await ShowPackagesAsync();
    await ShowQuoteAsync(request);

    return 0;
}
catch (FormatException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Usage: <length-mm> <breadth-mm> <height-mm> <weight-kg>");
    return 1;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Could not reach the API at {baseUrl}: {ex.Message}");
    Console.Error.WriteLine("Start it with: dotnet run --project src/Shipping.Api");
    return 1;
}

// Either the sample package, or four numbers supplied by the caller.
static QuoteRequest ParseQuoteRequest(string[] args)
{
    if (args.Length == 0)
    {
        return new QuoteRequest(200, 300, 150, 5m);
    }

    if (args.Length != 4)
    {
        throw new FormatException($"Expected 4 arguments but got {args.Length}.");
    }

    return new QuoteRequest(
        ParseInt(args[0], "length"),
        ParseInt(args[1], "breadth"),
        ParseInt(args[2], "height"),
        ParseDecimal(args[3], "weight"));
}

static int ParseInt(string value, string field) => int.TryParse(value, out var parsed)
    ? parsed
    : throw new FormatException($"'{value}' is not a whole number of millimetres for {field}.");

static decimal ParseDecimal(string value, string field) => decimal.TryParse(value, out var parsed)
    ? parsed
    : throw new FormatException($"'{value}' is not a valid {field}.");

async Task ShowPackagesAsync()
{
    var packageTypes =
        await http.GetFromJsonAsync<IReadOnlyList<PackageTypeResponse>>("/api/packages", json)
        ?? [];

    Console.WriteLine($"Package types at {baseUrl}");
    Console.WriteLine();

    foreach (var packageType in packageTypes)
    {
        var size = packageType.Dimensions;

        Console.WriteLine(
            $"  {packageType.Name,-8} " +
            $"{size.LengthMm,4} x {size.BreadthMm,4} x {size.HeightMm,4} mm   " +
            $"{packageType.Cost,6:0.00} NZD");
    }

    Console.WriteLine();
}

async Task ShowQuoteAsync(QuoteRequest request)
{
    Console.WriteLine(
        $"Quoting {request.LengthMm}x{request.BreadthMm}x{request.HeightMm}mm " +
        $"at {request.WeightKg}kg");
    Console.WriteLine();

    using var response = await http.PostAsJsonAsync("/api/quotes", request, json);

    if (response.IsSuccessStatusCode)
    {
        var quote = await response.Content.ReadFromJsonAsync<QuoteResponse>(json);

        if (quote is null)
        {
            Console.Error.WriteLine("The API returned an empty quote.");
            return;
        }

        Console.WriteLine($"  Package type : {quote.PackageType}");
        Console.WriteLine($"  Cost         : {quote.Cost:0.00} NZD");
        return;
    }

    var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(json);
    var detail = error?.Detail ?? response.ReasonPhrase ?? "The request failed.";

    // 422 means the request was fine but the package cannot be shipped - the most
    // interesting failure this API has, so name the reason rather than the status.
    if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
    {
        Console.WriteLine($"  No packaging solution ({error?.Reason}): {detail}");
        return;
    }

    Console.Error.WriteLine($"  {(int)response.StatusCode} {error?.Title}: {detail}");

    foreach (var message in error?.Errors ?? [])
    {
        Console.Error.WriteLine($"    - {message}");
    }
}
