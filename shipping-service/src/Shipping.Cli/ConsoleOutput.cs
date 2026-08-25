using System.Globalization;
using System.Text;
using Shipping.Contracts;

namespace Shipping.Cli;

/// <summary>
/// Formats results for a terminal. Pure string building, so the output is unit
/// testable without capturing the console.
/// </summary>
internal static class ConsoleOutput
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string PackageTable(IReadOnlyList<PackageTypeResponse> packageTypes)
    {
        if (packageTypes.Count == 0)
        {
            return "No package types are configured.";
        }

        string[] headers = ["NAME", "LENGTH", "BREADTH", "HEIGHT", "COST", "MAX WEIGHT", "ID"];

        var rows = packageTypes
            .Select(p => new[]
            {
                p.Name,
                Millimetres(p.Dimensions.LengthMm),
                Millimetres(p.Dimensions.BreadthMm),
                Millimetres(p.Dimensions.HeightMm),
                Cost(p.Cost),
                Kilograms(p.MaxWeightKg),
                p.Id.ToString(),
            })
            .ToList();

        var table = Table(headers, rows);

        return $"{table}{Environment.NewLine}Costs are in NZD.";
    }

    public static string PackageDetail(PackageTypeResponse packageType)
    {
        var builder = new StringBuilder();

        builder.AppendLine(Culture, $"Name       : {packageType.Name}");
        builder.AppendLine(Culture, $"Id         : {packageType.Id}");
        builder.AppendLine(
            Culture,
            $"Dimensions : {Millimetres(packageType.Dimensions.LengthMm)} x " +
            $"{Millimetres(packageType.Dimensions.BreadthMm)} x " +
            $"{Millimetres(packageType.Dimensions.HeightMm)} (LxBxH)");
        builder.AppendLine(Culture, $"Cost       : {Cost(packageType.Cost)} NZD");
        builder.Append(Culture, $"Max weight : {Kilograms(packageType.MaxWeightKg)}");

        return builder.ToString();
    }

    public static string Quote(QuoteResponse quote)
    {
        var builder = new StringBuilder();

        builder.AppendLine(Culture, $"Package type : {quote.PackageType}");
        builder.AppendLine(Culture, $"Cost         : {Cost(quote.Cost)} NZD");
        builder.AppendLine(
            Culture,
            $"Dimensions   : {Millimetres(quote.Dimensions.LengthMm)} x " +
            $"{Millimetres(quote.Dimensions.BreadthMm)} x " +
            $"{Millimetres(quote.Dimensions.HeightMm)} (LxBxH)");
        builder.Append(Culture, $"Weight       : {Kilograms(quote.WeightKg)}");

        return builder.ToString();
    }

    public static string Help() =>
        """
        shipping - command line client for the shipping packaging API

        Usage:
          shipping quote --length <mm> --breadth <mm> --height <mm> --weight <kg>
          shipping packages list
          shipping packages get <id|name>
          shipping packages add --name <name> --length <mm> --breadth <mm> --height <mm> --cost <amount>
          shipping packages update <id> [--name <name>] [--length <mm>]
                                        [--breadth <mm>] [--height <mm>] [--cost <amount>]
          shipping packages delete <id>

        Options:
          --api <url>   Base address of the API. Defaults to the SHIPPING_API_URL
                        environment variable, or http://localhost:5080.
          --help        Show this help.

        Examples:
          shipping quote --length 200 --breadth 300 --height 150 --weight 5
          shipping packages get small
          shipping packages add --name "Extra Large" --length 500 --breadth 700 --height 300 --cost 12.50
          shipping packages update 11111111-1111-1111-1111-111111111111 --cost 5.50

        Costs are in NZD. Packages over the published weight limit cannot be shipped.

        Exit codes:
          0  success
          1  bad usage
          2  API error
          3  no packaging solution available
        """;

    private static string Millimetres(int value) => $"{value.ToString(Culture)}mm";

    private static string Kilograms(decimal value) => $"{value.ToString("0.##", Culture)}kg";

    private static string Cost(decimal value) => value.ToString("0.00", Culture);

    private static string Table(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var widths = headers
            .Select((header, column) => rows
                .Select(row => row[column].Length)
                .Append(header.Length)
                .Max())
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine(Row(headers, widths));

        foreach (var row in rows)
        {
            builder.AppendLine(Row(row, widths));
        }

        return builder.ToString().TrimEnd();
    }

    private static string Row(IReadOnlyList<string> cells, IReadOnlyList<int> widths)
        => string.Join("  ", cells.Select((cell, column) => cell.PadRight(widths[column]))).TrimEnd();
}
