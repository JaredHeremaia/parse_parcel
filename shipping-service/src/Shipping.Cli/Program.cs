using Shipping.Cli;

const string DefaultApiUrl = "http://localhost:5080";

using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var parsed = CommandLineArgs.Parse(args);
    var baseAddress = ResolveApiAddress(parsed.GetString("api"));

    using var http = new HttpClient
    {
        BaseAddress = baseAddress,
        Timeout = TimeSpan.FromSeconds(30),
    };

    var runner = new CommandRunner(new ShippingApiClient(http), Console.Out);

    return await runner.RunAsync(parsed, cancellation.Token);
}
catch (CommandLineException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Run 'shipping --help' for usage.");
    return ExitCodes.UsageError;
}
catch (ApiException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ExitCodes.ApiError;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return ExitCodes.ApiError;
}

static Uri ResolveApiAddress(string? fromCommandLine)
{
    var raw = fromCommandLine
        ?? Environment.GetEnvironmentVariable("SHIPPING_API_URL")
        ?? DefaultApiUrl;

    if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
        || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        throw new CommandLineException($"'{raw}' is not a valid http(s) API address.");
    }

    // A trailing slash keeps relative request paths from replacing the last segment.
    return uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
}
