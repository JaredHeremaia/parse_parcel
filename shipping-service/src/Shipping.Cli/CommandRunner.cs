using Shipping.Contracts;

namespace Shipping.Cli;

internal static class ExitCodes
{
    public const int Success = 0;
    public const int UsageError = 1;
    public const int ApiError = 2;
    public const int NoPackagingSolution = 3;
}

/// <summary>
/// Maps parsed arguments onto API calls. Writes to an injected TextWriter so the
/// behaviour can be exercised without the console.
/// </summary>
internal sealed class CommandRunner
{
    private readonly ShippingApiClient _client;
    private readonly TextWriter _output;

    public CommandRunner(ShippingApiClient client, TextWriter output)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public async Task<int> RunAsync(CommandLineArgs args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        var command = args.Positional(0);

        if (command is null || args.HasOption("help") || IsHelp(command))
        {
            _output.WriteLine(ConsoleOutput.Help());
            return ExitCodes.Success;
        }

        return command.ToLowerInvariant() switch
        {
            "quote" => await QuoteAsync(args, cancellationToken),
            "packages" or "package" => await PackagesAsync(args, cancellationToken),
            _ => throw new CommandLineException(
                $"Unknown command '{command}'. Run 'shipping --help' to see the available commands."),
        };
    }

    private async Task<int> QuoteAsync(CommandLineArgs args, CancellationToken cancellationToken)
    {
        var request = new QuoteRequest(
            args.GetRequiredInt("length"),
            args.GetRequiredInt("breadth"),
            args.GetRequiredInt("height"),
            args.GetRequiredDecimal("weight"));

        var errors = request.Validate();
        if (errors.Count > 0)
        {
            throw new CommandLineException(string.Join(" ", errors));
        }

        var outcome = await _client.QuoteAsync(request, cancellationToken);

        if (!outcome.IsQuoted)
        {
            _output.WriteLine($"No packaging solution: {outcome.RejectionMessage}");
            return ExitCodes.NoPackagingSolution;
        }

        _output.WriteLine(ConsoleOutput.Quote(outcome.Quote!));
        return ExitCodes.Success;
    }

    private async Task<int> PackagesAsync(CommandLineArgs args, CancellationToken cancellationToken)
    {
        var subCommand = args.Positional(1)?.ToLowerInvariant();

        return subCommand switch
        {
            null or "list" => await ListAsync(cancellationToken),
            "get" => await GetAsync(args, cancellationToken),
            "add" => await AddAsync(args, cancellationToken),
            "update" => await UpdateAsync(args, cancellationToken),
            "delete" or "remove" => await DeleteAsync(args, cancellationToken),
            _ => throw new CommandLineException(
                $"Unknown packages sub-command '{subCommand}'. " +
                "Expected list, get, add, update or delete."),
        };
    }

    private async Task<int> ListAsync(CancellationToken cancellationToken)
    {
        var packageTypes = await _client.ListAsync(cancellationToken);

        _output.WriteLine(ConsoleOutput.PackageTable(packageTypes));
        return ExitCodes.Success;
    }

    private async Task<int> GetAsync(CommandLineArgs args, CancellationToken cancellationToken)
    {
        var key = args.Positional(2)
            ?? throw new CommandLineException("Provide a package type id or name, e.g. 'shipping packages get small'.");

        var packageType = await _client.GetAsync(key, cancellationToken);

        _output.WriteLine(ConsoleOutput.PackageDetail(packageType));
        return ExitCodes.Success;
    }

    private async Task<int> AddAsync(CommandLineArgs args, CancellationToken cancellationToken)
    {
        var request = new PackageTypeRequest(
            args.GetRequiredString("name"),
            args.GetRequiredInt("length"),
            args.GetRequiredInt("breadth"),
            args.GetRequiredInt("height"),
            args.GetRequiredDecimal("cost"));

        var errors = request.Validate();
        if (errors.Count > 0)
        {
            throw new CommandLineException(string.Join(" ", errors));
        }

        var created = await _client.CreateAsync(request, cancellationToken);

        _output.WriteLine($"Added package type '{created.Name}'.");
        _output.WriteLine(ConsoleOutput.PackageDetail(created));
        return ExitCodes.Success;
    }

    /// <summary>
    /// PUT replaces the whole package type, so unspecified fields are read back
    /// from the API first. That lets the user change one field without restating
    /// the rest.
    /// </summary>
    private async Task<int> UpdateAsync(CommandLineArgs args, CancellationToken cancellationToken)
    {
        var id = args.GetRequiredGuid("id", 2);

        var name = args.GetString("name");
        var length = args.GetInt("length");
        var breadth = args.GetInt("breadth");
        var height = args.GetInt("height");
        var cost = args.GetDecimal("cost");

        if (name is null && length is null && breadth is null && height is null && cost is null)
        {
            throw new CommandLineException(
                "Provide at least one of --name, --length, --breadth, --height or --cost.");
        }

        var existing = await _client.GetAsync(id.ToString(), cancellationToken);

        var request = new PackageTypeRequest(
            name ?? existing.Name,
            length ?? existing.Dimensions.LengthMm,
            breadth ?? existing.Dimensions.BreadthMm,
            height ?? existing.Dimensions.HeightMm,
            cost ?? existing.Cost);

        var errors = request.Validate();
        if (errors.Count > 0)
        {
            throw new CommandLineException(string.Join(" ", errors));
        }

        var updated = await _client.UpdateAsync(id, request, cancellationToken);

        _output.WriteLine($"Updated package type '{updated.Name}'.");
        _output.WriteLine(ConsoleOutput.PackageDetail(updated));
        return ExitCodes.Success;
    }

    private async Task<int> DeleteAsync(CommandLineArgs args, CancellationToken cancellationToken)
    {
        var id = args.GetRequiredGuid("id", 2);

        await _client.DeleteAsync(id, cancellationToken);

        _output.WriteLine($"Deleted package type {id}.");
        return ExitCodes.Success;
    }

    private static bool IsHelp(string command)
        => command.Equals("help", StringComparison.OrdinalIgnoreCase)
        || command.Equals("--help", StringComparison.OrdinalIgnoreCase)
        || command.Equals("-h", StringComparison.OrdinalIgnoreCase);
}
