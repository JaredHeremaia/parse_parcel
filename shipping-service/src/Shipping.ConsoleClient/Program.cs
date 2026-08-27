using Shipping.ConsoleClient;

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

var baseUrl = Environment.GetEnvironmentVariable("SHIPPING_API_URL") ?? DefaultApiUrl;

// Decoration only, so keep it out of a pipe or a redirect to a file.
if (!Console.IsOutputRedirected)
{
    Console.WriteLine(Banner.Art);
    Console.WriteLine();
}

using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

var client = new ShippingConsoleClient(http, Console.Out, Console.Error);

return await client.RunAsync(args);
