using Shipping.Core.Model;
using Shipping.Core.Quoting;
using Shipping.Core.Storage;
using Xunit;

namespace Shipping.Core.Tests;

public class PackagingCalculatorTests
{
    private static PackagingCalculator CreateCalculator(
        IEnumerable<PackageType>? catalogue = null,
        PackagingOptions? options = null)
        => new(new InMemoryPackageTypeStore(catalogue), options);

    [Theory]
    // Anything comfortably inside the small box.
    [InlineData(100, 100, 100, "Small", 5.00)]
    // Exactly the published small dimensions.
    [InlineData(200, 300, 150, "Small", 5.00)]
    // One millimetre over small on each side in turn moves up to medium.
    [InlineData(201, 300, 150, "Medium", 7.50)]
    [InlineData(200, 301, 150, "Medium", 7.50)]
    [InlineData(200, 300, 151, "Medium", 7.50)]
    // Exactly the published medium dimensions.
    [InlineData(300, 400, 200, "Medium", 7.50)]
    // One millimetre over medium moves up to large.
    [InlineData(301, 400, 200, "Large", 8.50)]
    [InlineData(300, 401, 200, "Large", 8.50)]
    [InlineData(300, 400, 201, "Large", 8.50)]
    // Exactly the published large dimensions.
    [InlineData(400, 600, 250, "Large", 8.50)]
    public async Task The_smallest_suitable_package_type_is_quoted(
        int length,
        int breadth,
        int height,
        string expectedName,
        decimal expectedCost)
    {
        var calculator = CreateCalculator();

        var result = await calculator.QuoteAsync(new Dimensions(length, breadth, height), 1m);

        Assert.True(result.IsQuoted);
        Assert.Equal(expectedName, result.Quote.PackageTypeName);
        Assert.Equal(expectedCost, result.Quote.Cost);
    }

    [Theory]
    [InlineData(401, 600, 250)]
    [InlineData(400, 601, 250)]
    [InlineData(400, 600, 251)]
    [InlineData(1000, 1000, 1000)]
    public async Task A_package_larger_than_every_box_gets_no_solution(int length, int breadth, int height)
    {
        var calculator = CreateCalculator();

        var result = await calculator.QuoteAsync(new Dimensions(length, breadth, height), 1m);

        Assert.False(result.IsQuoted);
        Assert.Equal(QuoteRejectionReason.Oversized, result.RejectionReason);
        Assert.Throws<InvalidOperationException>(() => result.Quote);
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(1)]
    [InlineData(24.99)]
    [InlineData(25)] // exactly the limit is still acceptable
    public async Task A_package_at_or_under_the_weight_limit_is_quoted(decimal weightKg)
    {
        var calculator = CreateCalculator();

        var result = await calculator.QuoteAsync(new Dimensions(100, 100, 100), weightKg);

        Assert.True(result.IsQuoted);
        Assert.Equal(weightKg, result.Quote.WeightKg);
    }

    [Theory]
    [InlineData(25.01)]
    [InlineData(26)]
    [InlineData(1000)]
    public async Task A_package_over_the_weight_limit_gets_no_solution(decimal weightKg)
    {
        var calculator = CreateCalculator();

        var result = await calculator.QuoteAsync(new Dimensions(100, 100, 100), weightKg);

        Assert.False(result.IsQuoted);
        Assert.Equal(QuoteRejectionReason.Overweight, result.RejectionReason);
        Assert.Contains("25", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public async Task A_weight_of_zero_or_less_is_invalid_input(decimal weightKg)
    {
        var calculator = CreateCalculator();

        var result = await calculator.QuoteAsync(new Dimensions(100, 100, 100), weightKg);

        Assert.False(result.IsQuoted);
        Assert.Equal(QuoteRejectionReason.InvalidInput, result.RejectionReason);
    }

    [Fact]
    public async Task Weight_is_checked_before_size_so_the_message_names_the_first_blocker()
    {
        var calculator = CreateCalculator();

        var result = await calculator.QuoteAsync(new Dimensions(5000, 5000, 5000), 500m);

        Assert.Equal(QuoteRejectionReason.Overweight, result.RejectionReason);
    }

    [Fact]
    public async Task A_package_may_be_turned_to_fit_a_smaller_box()
    {
        var calculator = CreateCalculator();

        // The small box is 200x300x150; this is the same box in a different order.
        var result = await calculator.QuoteAsync(new Dimensions(300, 150, 200), 1m);

        Assert.True(result.IsQuoted);
        Assert.Equal("Small", result.Quote.PackageTypeName);
    }

    [Fact]
    public async Task Rotation_can_be_switched_off_by_configuration()
    {
        var calculator = CreateCalculator(options: new PackagingOptions { AllowRotation = false });

        var result = await calculator.QuoteAsync(new Dimensions(300, 150, 200), 1m);

        Assert.True(result.IsQuoted);
        Assert.Equal("Medium", result.Quote.PackageTypeName);
    }

    [Fact]
    public async Task The_weight_limit_is_configurable()
    {
        var calculator = CreateCalculator(options: new PackagingOptions { MaxWeightKg = 10m });

        var accepted = await calculator.QuoteAsync(new Dimensions(100, 100, 100), 10m);
        var rejected = await calculator.QuoteAsync(new Dimensions(100, 100, 100), 10.01m);

        Assert.True(accepted.IsQuoted);
        Assert.Equal(QuoteRejectionReason.Overweight, rejected.RejectionReason);
    }

    [Fact]
    public async Task The_cheapest_suitable_package_type_wins_even_if_a_smaller_one_fits()
    {
        var catalogue = new[]
        {
            new PackageType(Guid.NewGuid(), "Snug", new Dimensions(100, 100, 100), 9.00m),
            new PackageType(Guid.NewGuid(), "Roomy", new Dimensions(500, 500, 500), 4.00m),
        };

        var result = await CreateCalculator(catalogue).QuoteAsync(new Dimensions(50, 50, 50), 1m);

        Assert.Equal("Roomy", result.Quote.PackageTypeName);
        Assert.Equal(4.00m, result.Quote.Cost);
    }

    [Fact]
    public async Task When_costs_tie_the_smaller_box_wins()
    {
        var catalogue = new[]
        {
            new PackageType(Guid.NewGuid(), "Roomy", new Dimensions(500, 500, 500), 5.00m),
            new PackageType(Guid.NewGuid(), "Snug", new Dimensions(100, 100, 100), 5.00m),
        };

        var result = await CreateCalculator(catalogue).QuoteAsync(new Dimensions(50, 50, 50), 1m);

        Assert.Equal("Snug", result.Quote.PackageTypeName);
    }

    [Fact]
    public async Task An_empty_catalogue_yields_no_solution_rather_than_an_error()
    {
        var result = await CreateCalculator([]).QuoteAsync(new Dimensions(10, 10, 10), 1m);

        Assert.False(result.IsQuoted);
        Assert.Equal(QuoteRejectionReason.Oversized, result.RejectionReason);
    }

    [Fact]
    public async Task The_quote_reports_what_was_asked_for()
    {
        var dimensions = new Dimensions(150, 150, 100);

        var result = await CreateCalculator().QuoteAsync(dimensions, 3.5m);

        Assert.Equal(dimensions, result.Quote.Dimensions);
        Assert.Equal(3.5m, result.Quote.WeightKg);
        Assert.Equal(StandardPackageTypes.SmallId, result.Quote.PackageTypeId);
    }

    [Fact]
    public async Task Dimensions_are_required()
        => await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreateCalculator().QuoteAsync(null!, 1m));

    [Fact]
    public void A_store_is_required()
        => Assert.Throws<ArgumentNullException>(() => new PackagingCalculator(null!));

    [Fact]
    public void A_nonsensical_weight_limit_is_rejected_at_construction()
        => Assert.Throws<InvalidOperationException>(
            () => CreateCalculator(options: new PackagingOptions { MaxWeightKg = 0m }));
}
