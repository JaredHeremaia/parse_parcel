using System.Globalization;
using Shipping.Contracts;

namespace Shipping.ConsoleClient;

/// <summary>
/// Turns the command line into a command and its arguments. Numbers are parsed with the
/// invariant culture so the same input means the same thing on a machine that writes 5,5.
/// </summary>
internal static class CommandLine
{
    public const string Usage = """
        Usage:
          (no arguments)                                            list the catalogue, quote the sample package
          list                                                      every package type
          quote  <length> <breadth> <height> <weight>               advise on cost and package type
          add    <name> <length> <breadth> <height> <cost>          add a package type
          update <id> [options]                                     change part of a package type
          delete <id>                                               remove a package type
          help                                                      show this

        Update options, at least one required. Anything left out keeps its current value:
          --name <name>   --length <mm>   --breadth <mm>   --height <mm>   --cost <amount>

        Lengths are whole millimetres, weight is kilograms, cost is NZD.
        A name containing spaces needs quoting: add "Extra Large" 500 700 300 12.50

        The API address comes from SHIPPING_API_URL, default http://localhost:5080.
        Exit codes: 0 success, 1 bad usage or unreachable API, 2 the API rejected the request.
        """;

    /// <summary>The sample package quoted when nothing is supplied.</summary>
    public static QuoteRequest Sample { get; } = new(200, 300, 150, 5m);

    public static QuoteRequest ParseQuote(string[] args)
    {
        Require(args, 5, "quote <length> <breadth> <height> <weight>");

        return new QuoteRequest(
            ParseInt(args[1], "length"),
            ParseInt(args[2], "breadth"),
            ParseInt(args[3], "height"),
            ParseDecimal(args[4], "weight"));
    }

    public static PackageTypeRequest ParseAdd(string[] args)
    {
        Require(args, 6, "add <name> <length> <breadth> <height> <cost>");

        return ParsePackage(args, offset: 1);
    }

    /// <summary>
    /// Update is a PATCH, so only the options given are sent and everything else keeps its
    /// current value. Options are named because a partial change cannot be positional.
    /// </summary>
    public static (Guid Id, PackageTypePatchRequest Request) ParseUpdate(string[] args)
    {
        if (args.Length < 3)
        {
            throw new FormatException(
                "'update' needs an id and at least one option. Expected: " +
                "update <id> [--name <name>] [--length <mm>] [--breadth <mm>] [--height <mm>] [--cost <amount>]");
        }

        var id = ParseGuid(args[1]);

        string? name = null;
        int? length = null;
        int? breadth = null;
        int? height = null;
        decimal? cost = null;

        for (var i = 2; i < args.Length; i += 2)
        {
            var option = args[i].ToLowerInvariant();

            // Check the option is one we know before asking for its value, so an unknown
            // flag is reported as unknown rather than as a missing value.
            if (option is not ("--name" or "--length" or "--breadth" or "--height" or "--cost"))
            {
                throw new FormatException(
                    $"Unknown option '{args[i]}'. Expected --name, --length, --breadth, --height or --cost.");
            }

            if (i + 1 >= args.Length)
            {
                throw new FormatException($"'{args[i]}' needs a value.");
            }

            var value = args[i + 1];

            switch (option)
            {
                case "--name": name = value; break;
                case "--length": length = ParseInt(value, "length"); break;
                case "--breadth": breadth = ParseInt(value, "breadth"); break;
                case "--height": height = ParseInt(value, "height"); break;
                default: cost = ParseDecimal(value, "cost"); break;
            }
        }

        return (id, new PackageTypePatchRequest(name, length, breadth, height, cost));
    }

    public static Guid ParseDelete(string[] args)
    {
        Require(args, 2, "delete <id>");

        return ParseGuid(args[1]);
    }

    private static PackageTypeRequest ParsePackage(string[] args, int offset)
        => new(
            args[offset],
            ParseInt(args[offset + 1], "length"),
            ParseInt(args[offset + 2], "breadth"),
            ParseInt(args[offset + 3], "height"),
            ParseDecimal(args[offset + 4], "cost"));

    private static void Require(string[] args, int expected, string form)
    {
        if (args.Length == expected)
        {
            return;
        }

        throw new FormatException(
            $"'{args[0]}' takes {expected - 1} arguments but got {args.Length - 1}. Expected: {form}");
    }

    private static Guid ParseGuid(string value)
        => Guid.TryParse(value, out var id)
            ? id
            : throw new FormatException($"'{value}' is not a valid id.");

    private static int ParseInt(string value, string field)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException(
                $"'{value}' is not a whole number of millimetres for {field}.");

    private static decimal ParseDecimal(string value, string field)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"'{value}' is not a valid {field}.");
}
