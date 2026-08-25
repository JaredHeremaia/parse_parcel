using Shipping.Cli;
using Xunit;

namespace Shipping.Cli.Tests;

public class CommandLineArgsTests
{
    [Fact]
    public void Words_before_options_are_positional()
    {
        var args = CommandLineArgs.Parse(["packages", "get", "small"]);

        Assert.Equal(new[] { "packages", "get", "small" }, args.Positionals.ToArray());
        Assert.Equal("packages", args.Positional(0));
        Assert.Equal("small", args.Positional(2));
        Assert.Null(args.Positional(3));
    }

    [Fact]
    public void Options_can_be_written_with_a_space()
    {
        var args = CommandLineArgs.Parse(["quote", "--length", "200"]);

        Assert.Equal(200, args.GetInt("length"));
    }

    [Fact]
    public void Options_can_be_written_with_an_equals_sign()
    {
        var args = CommandLineArgs.Parse(["quote", "--length=200"]);

        Assert.Equal(200, args.GetInt("length"));
    }

    [Fact]
    public void Option_names_are_case_insensitive()
    {
        var args = CommandLineArgs.Parse(["--Length", "200"]);

        Assert.Equal(200, args.GetInt("length"));
    }

    [Fact]
    public void An_option_with_no_value_is_a_flag()
    {
        var args = CommandLineArgs.Parse(["packages", "--help"]);

        Assert.True(args.HasOption("help"));
        Assert.False(args.HasOption("verbose"));
    }

    [Fact]
    public void A_flag_followed_by_another_option_keeps_its_flag_value()
    {
        var args = CommandLineArgs.Parse(["--help", "--api", "http://localhost:5080"]);

        Assert.True(args.HasOption("help"));
        Assert.Equal("http://localhost:5080", args.GetString("api"));
    }

    [Fact]
    public void Values_containing_spaces_survive_intact()
    {
        var args = CommandLineArgs.Parse(["packages", "add", "--name", "Extra Large"]);

        Assert.Equal("Extra Large", args.GetString("name"));
    }

    [Fact]
    public void A_later_value_wins_when_an_option_is_repeated()
    {
        var args = CommandLineArgs.Parse(["--cost", "5", "--cost", "7"]);

        Assert.Equal(7m, args.GetDecimal("cost"));
    }

    [Fact]
    public void An_empty_option_name_is_rejected()
        => Assert.Throws<CommandLineException>(() => CommandLineArgs.Parse(["--=5"]));

    [Fact]
    public void Missing_optional_values_come_back_as_null()
    {
        var args = CommandLineArgs.Parse(["packages", "list"]);

        Assert.Null(args.GetString("name"));
        Assert.Null(args.GetInt("length"));
        Assert.Null(args.GetDecimal("cost"));
    }

    [Fact]
    public void A_required_option_that_is_missing_is_a_usage_error()
    {
        var args = CommandLineArgs.Parse(["quote"]);

        var error = Assert.Throws<CommandLineException>(() => args.GetRequiredInt("length"));
        Assert.Contains("--length", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("")]
    public void A_non_numeric_whole_number_is_a_usage_error(string value)
    {
        var args = CommandLineArgs.Parse(["quote", $"--length={value}"]);

        Assert.Throws<CommandLineException>(() => args.GetInt("length"));
    }

    [Fact]
    public void A_non_numeric_decimal_is_a_usage_error()
    {
        var args = CommandLineArgs.Parse(["quote", "--weight", "heavy"]);

        Assert.Throws<CommandLineException>(() => args.GetDecimal("weight"));
    }

    [Fact]
    public void Decimals_are_parsed_with_invariant_formatting()
    {
        var args = CommandLineArgs.Parse(["--cost", "12.50"]);

        Assert.Equal(12.50m, args.GetDecimal("cost"));
    }

    [Fact]
    public void Negative_numbers_are_parsed_rather_than_treated_as_options()
    {
        var args = CommandLineArgs.Parse(["--length", "-5"]);

        Assert.Equal(-5, args.GetInt("length"));
    }

    [Fact]
    public void A_required_id_is_read_from_the_expected_position()
    {
        var id = Guid.NewGuid();
        var args = CommandLineArgs.Parse(["packages", "delete", id.ToString()]);

        Assert.Equal(id, args.GetRequiredGuid("id", 2));
    }

    [Fact]
    public void A_missing_id_is_a_usage_error()
    {
        var args = CommandLineArgs.Parse(["packages", "delete"]);

        Assert.Throws<CommandLineException>(() => args.GetRequiredGuid("id", 2));
    }

    [Fact]
    public void A_malformed_id_is_a_usage_error()
    {
        var args = CommandLineArgs.Parse(["packages", "delete", "not-a-guid"]);

        var error = Assert.Throws<CommandLineException>(() => args.GetRequiredGuid("id", 2));
        Assert.Contains("not-a-guid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_arguments_at_all_parses_to_nothing()
    {
        var args = CommandLineArgs.Parse([]);

        Assert.Empty(args.Positionals);
        Assert.Empty(args.Options);
    }
}
