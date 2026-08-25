using Shipping.Cli;
using Shipping.Contracts;
using Xunit;

namespace Shipping.Cli.Tests;

public class ConsoleOutputTests
{
    private static PackageTypeResponse Small => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Small",
        new DimensionsResponse(200, 300, 150, 9_000_000),
        5.00m,
        25m);

    [Fact]
    public void The_table_has_a_header_and_a_row_for_each_package_type()
    {
        var table = ConsoleOutput.PackageTable([Small]);
        var lines = table.Split(Environment.NewLine);

        Assert.Contains("NAME", lines[0], StringComparison.Ordinal);
        Assert.Contains("COST", lines[0], StringComparison.Ordinal);
        Assert.Contains("Small", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Costs_are_shown_to_two_decimal_places_without_a_symbol()
    {
        var table = ConsoleOutput.PackageTable([Small]);

        Assert.Contains("5.00", table, StringComparison.Ordinal);
        Assert.DoesNotContain("$", table, StringComparison.Ordinal);
    }

    [Fact]
    public void The_currency_is_stated_once_rather_than_on_every_number()
    {
        var table = ConsoleOutput.PackageTable([Small]);

        Assert.EndsWith("Costs are in NZD.", table, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_catalogue_says_so_instead_of_printing_an_empty_table()
        => Assert.Equal("No package types are configured.", ConsoleOutput.PackageTable([]));

    [Fact]
    public void Detail_output_covers_every_field()
    {
        var detail = ConsoleOutput.PackageDetail(Small);

        Assert.Contains("Small", detail, StringComparison.Ordinal);
        Assert.Contains("11111111-1111-1111-1111-111111111111", detail, StringComparison.Ordinal);
        Assert.Contains("200mm x 300mm x 150mm", detail, StringComparison.Ordinal);
        Assert.Contains("5.00 NZD", detail, StringComparison.Ordinal);
        Assert.Contains("25kg", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_quote_shows_the_package_type_cost_and_what_was_measured()
    {
        var quote = new QuoteResponse(
            Guid.NewGuid(),
            "Medium",
            7.50m,
            new DimensionsResponse(250, 350, 180, 15_750_000),
            12.5m);

        var rendered = ConsoleOutput.Quote(quote);

        Assert.Contains("Medium", rendered, StringComparison.Ordinal);
        Assert.Contains("7.50 NZD", rendered, StringComparison.Ordinal);
        Assert.Contains("250mm x 350mm x 180mm", rendered, StringComparison.Ordinal);
        Assert.Contains("12.5kg", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Columns_line_up_when_names_differ_in_length()
    {
        var large = new PackageTypeResponse(
            Guid.NewGuid(),
            "Considerably Larger",
            new DimensionsResponse(400, 600, 250, 60_000_000),
            8.50m,
            25m);

        var lines = ConsoleOutput.PackageTable([Small, large]).Split(Environment.NewLine);

        var firstRowCostIndex = lines[1].IndexOf("5.00", StringComparison.Ordinal);
        var secondRowCostIndex = lines[2].IndexOf("8.50", StringComparison.Ordinal);

        Assert.Equal(firstRowCostIndex, secondRowCostIndex);
    }

    [Fact]
    public void Help_lists_every_command_and_the_exit_codes()
    {
        var help = ConsoleOutput.Help();

        Assert.Contains("shipping quote", help, StringComparison.Ordinal);
        Assert.Contains("packages list", help, StringComparison.Ordinal);
        Assert.Contains("packages get", help, StringComparison.Ordinal);
        Assert.Contains("packages add", help, StringComparison.Ordinal);
        Assert.Contains("packages update", help, StringComparison.Ordinal);
        Assert.Contains("packages delete", help, StringComparison.Ordinal);
        Assert.Contains("--api", help, StringComparison.Ordinal);
        Assert.Contains("Exit codes:", help, StringComparison.Ordinal);
    }
}
