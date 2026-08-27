using System.Globalization;
using Shipping.Contracts;

namespace Shipping.ConsoleClient;

/// <summary>
/// Turns the command line into a quote request. Numbers are parsed with the invariant
/// culture so the same arguments mean the same thing on a machine that writes 5,5.
/// </summary>
internal static class QuoteArguments
{
    public const string Usage = "Usage: <length-mm> <breadth-mm> <height-mm> <weight-kg>";

    /// <summary>The sample package used when nothing is supplied.</summary>
    public static QuoteRequest Sample { get; } = new(200, 300, 150, 5m);

    public static QuoteRequest Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return Sample;
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
