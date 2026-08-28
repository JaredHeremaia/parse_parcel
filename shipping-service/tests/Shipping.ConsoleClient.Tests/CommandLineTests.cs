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
    public void Update_reads_the_id_then_named_options()
    {
        var (id, request) = CommandLine.ParseUpdate(
            ["update", "11111111-1111-1111-1111-111111111111", "--name", "Small", "--cost", "6.00"]);

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), id);
        Assert.Equal("Small", request.Name);
        Assert.Equal(6.00m, request.Cost);

        // Everything not named is absent, so the API leaves it alone.
        Assert.Null(request.LengthMm);
        Assert.Null(request.BreadthMm);
        Assert.Null(request.HeightMm);
    }

    [Fact]
    public void Update_accepts_a_single_option()
    {
        var (_, request) = CommandLine.ParseUpdate(
            ["update", "11111111-1111-1111-1111-111111111111", "--cost", "6.00"]);

        Assert.Equal(6.00m, request.Cost);
        Assert.False(request.IsEmpty);
    }

    [Fact]
    public void Update_reads_every_option()
    {
        var (_, request) = CommandLine.ParseUpdate([
            "update", "11111111-1111-1111-1111-111111111111",
            "--name", "Small", "--length", "210", "--breadth", "310", "--height", "160", "--cost", "5.50",
        ]);

        Assert.Equal("Small", request.Name);
        Assert.Equal(210, request.LengthMm);
        Assert.Equal(310, request.BreadthMm);
        Assert.Equal(160, request.HeightMm);
        Assert.Equal(5.50m, request.Cost);
    }

    [Fact]
    public void Update_with_no_options_is_rejected()
    {
        var ex = Assert.Throws<FormatException>(
            () => CommandLine.ParseUpdate(["update", "11111111-1111-1111-1111-111111111111"]));

        Assert.Contains("at least one option", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_update_option_is_rejected()
    {
        var ex = Assert.Throws<FormatException>(() => CommandLine.ParseUpdate(
            ["update", "11111111-1111-1111-1111-111111111111", "--colour", "red"]));

        Assert.Contains("Unknown option '--colour'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_update_option_without_a_value_is_rejected()
    {
        var ex = Assert.Throws<FormatException>(() => CommandLine.ParseUpdate(
            ["update", "11111111-1111-1111-1111-111111111111", "--cost"]));

        Assert.Contains("needs a value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Delete_reads_the_id()
        => Assert.Equal(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CommandLine.ParseDelete(["delete", "11111111-1111-1111-1111-111111111111"]));

    [Fact]
    public void A_delete_id_that_is_not_a_guid_is_rejected()
    {
        var ex = Assert.Throws<FormatException>(
            () => CommandLine.ParseDelete(["delete", "not-a-guid"]));

        Assert.Contains("not a valid id", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_update_id_that_is_not_a_guid_is_rejected()
    {
        var ex = Assert.Throws<FormatException>(
            () => CommandLine.ParseUpdate(["update", "12345", "--cost", "6.00"]));

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
