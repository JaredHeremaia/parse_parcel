using Xunit;

namespace Shipping.ConsoleClient.Tests;

public class QuoteArgumentsTests
{
    [Fact]
    public void No_arguments_quotes_the_sample_package()
    {
        var request = QuoteArguments.Parse([]);

        Assert.Equal(200, request.LengthMm);
        Assert.Equal(300, request.BreadthMm);
        Assert.Equal(150, request.HeightMm);
        Assert.Equal(5m, request.WeightKg);
    }

    [Fact]
    public void Four_arguments_are_read_as_length_breadth_height_and_weight()
    {
        var request = QuoteArguments.Parse(["201", "300", "150", "5"]);

        Assert.Equal(201, request.LengthMm);
        Assert.Equal(300, request.BreadthMm);
        Assert.Equal(150, request.HeightMm);
        Assert.Equal(5m, request.WeightKg);
    }

    [Fact]
    public void Weight_may_be_fractional()
        => Assert.Equal(5.5m, QuoteArguments.Parse(["200", "300", "150", "5.5"]).WeightKg);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void Any_count_other_than_four_is_rejected(int count)
    {
        var args = Enumerable.Repeat("1", count).ToArray();

        var ex = Assert.Throws<FormatException>(() => QuoteArguments.Parse(args));

        Assert.Contains($"got {count}", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "length")]
    [InlineData(1, "breadth")]
    [InlineData(2, "height")]
    public void A_side_that_is_not_a_number_names_the_field(int position, string field)
    {
        string[] args = ["200", "300", "150", "5"];
        args[position] = "abc";

        var ex = Assert.Throws<FormatException>(() => QuoteArguments.Parse(args));

        Assert.Contains(field, ex.Message, StringComparison.Ordinal);
        Assert.Contains("'abc'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_weight_that_is_not_a_number_names_the_field()
    {
        var ex = Assert.Throws<FormatException>(
            () => QuoteArguments.Parse(["200", "300", "150", "heavy"]));

        Assert.Contains("weight", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_fractional_side_is_rejected_rather_than_truncated()
        => Assert.Throws<FormatException>(() => QuoteArguments.Parse(["200.5", "300", "150", "5"]));
}
