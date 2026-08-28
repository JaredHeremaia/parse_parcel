using Xunit;

namespace Shipping.ConsoleClient.Tests;

public class CommandLineTests
{
    [Fact]
    public void The_sample_package_is_the_published_small_box()
    {
        Assert.Equal(200, CommandLine.Sample.LengthMm);
        Assert.Equal(300, CommandLine.Sample.BreadthMm);
        Assert.Equal(150, CommandLine.Sample.HeightMm);
        Assert.Equal(5m, CommandLine.Sample.WeightKg);
    }

    [Fact]
    public void Quote_reads_length_breadth_height_and_weight()
    {
        var request = CommandLine.ParseQuote(["quote", "201", "300", "150", "5"]);

        Assert.Equal(201, request.LengthMm);
        Assert.Equal(300, request.BreadthMm);
        Assert.Equal(150, request.HeightMm);
        Assert.Equal(5m, request.WeightKg);
    }

    [Fact]
    public void Weight_may_be_fractional()
        => Assert.Equal(5.5m, CommandLine.ParseQuote(["quote", "200", "300", "150", "5.5"]).WeightKg);

    [Fact]
    public void Add_reads_name_then_dimensions_then_cost()
    {
        var request = CommandLine.ParseAdd(["add", "Extra Large", "500", "700", "300", "12.50"]);

        Assert.Equal("Extra Large", request.Name);
        Assert.Equal(500, request.LengthMm);
        Assert.Equal(700, request.BreadthMm);
        Assert.Equal(300, request.HeightMm);
        Assert.Equal(12.50m, request.Cost);
    }

    [Fact]
    public void Update_reads_the_id_then_a_complete_package_type()
    {
        var (id, request) = CommandLine.ParseUpdate(
            ["update", "11111111-1111-1111-1111-111111111111", "Small", "200", "300", "150", "6.00"]);

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), id);
        Assert.Equal("Small", request.Name);
        Assert.Equal(200, request.LengthMm);
        Assert.Equal(6.00m, request.Cost);
    }

    [Fact]
    public void Delete_reads_the_id()
        => Assert.Equal(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CommandLine.ParseDelete(["delete", "11111111-1111-1111-1111-111111111111"]));

    [Theory]
    [InlineData("delete", "not-a-guid")]
    [InlineData("update", "12345")]
    public void An_id_that_is_not_a_guid_is_rejected(string command, string id)
    {
        string[] args = command == "delete"
            ? [command, id]
            : [command, id, "Small", "200", "300", "150", "6.00"];

        var ex = Assert.Throws<FormatException>(() => command == "delete"
            ? CommandLine.ParseDelete(args)
            : CommandLine.ParseUpdate(args).Id);

        Assert.Contains("not a valid id", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Quote_with_too_few_arguments_is_rejected()
    {
        var ex = Assert.Throws<FormatException>(
            () => CommandLine.ParseQuote(["quote", "200", "300", "150"]));

        Assert.Contains("takes 4 arguments but got 3", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Quote_with_too_many_arguments_is_rejected()
    {
        var ex = Assert.Throws<FormatException>(
            () => CommandLine.ParseQuote(["quote", "200", "300", "150", "5", "6"]));

        Assert.Contains("takes 4 arguments but got 5", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_with_the_wrong_number_of_arguments_names_the_expected_form()
    {
        var ex = Assert.Throws<FormatException>(
            () => CommandLine.ParseAdd(["add", "Extra Large", "500", "700"]));

        Assert.Contains("takes 5 arguments", ex.Message, StringComparison.Ordinal);
        Assert.Contains("add <name> <length> <breadth> <height> <cost>", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_with_the_wrong_number_of_arguments_names_the_expected_form()
    {
        var ex = Assert.Throws<FormatException>(
            () => CommandLine.ParseUpdate(["update", "11111111-1111-1111-1111-111111111111", "Small"]));

        Assert.Contains("takes 6 arguments", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, "length")]
    [InlineData(2, "breadth")]
    [InlineData(3, "height")]
    public void A_side_that_is_not_a_number_names_the_field(int position, string field)
    {
        string[] args = ["quote", "200", "300", "150", "5"];
        args[position] = "abc";

        var ex = Assert.Throws<FormatException>(() => CommandLine.ParseQuote(args));

        Assert.Contains(field, ex.Message, StringComparison.Ordinal);
        Assert.Contains("'abc'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cost_that_is_not_a_number_names_the_field()
    {
        var ex = Assert.Throws<FormatException>(
            () => CommandLine.ParseAdd(["add", "Extra Large", "500", "700", "300", "free"]));

        Assert.Contains("cost", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_fractional_side_is_rejected_rather_than_truncated()
        => Assert.Throws<FormatException>(
            () => CommandLine.ParseQuote(["quote", "200.5", "300", "150", "5"]));
}
