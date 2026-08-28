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
          update <id> <name> <length> <breadth> <height> <cost>     replace a package type
          delete <id>                                               remove a package type
          help                                                      show this

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
    /// PUT replaces the whole package type, so every field is required rather than merged
    /// into the existing one. What you type is what the package type becomes.
    /// </summary>
    public static (Guid Id, PackageTypeRequest Request) ParseUpdate(string[] args)
    {
        Require(args, 7, "update <id> <name> <length> <breadth> <height> <cost>");

        return (ParseGuid(args[1]), ParsePackage(args, offset: 2));
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
