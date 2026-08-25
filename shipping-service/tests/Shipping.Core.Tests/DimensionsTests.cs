using Shipping.Core.Model;
using Xunit;

namespace Shipping.Core.Tests;

public class DimensionsTests
{
    [Theory]
    [InlineData(0, 300, 150)]
    [InlineData(200, 0, 150)]
    [InlineData(200, 300, 0)]
    [InlineData(-1, 300, 150)]
    [InlineData(200, -1, 150)]
    [InlineData(200, 300, -1)]
    [InlineData(Dimensions.MaxSideMm + 1, 300, 150)]
    public void Constructor_rejects_sides_outside_the_allowed_range(int length, int breadth, int height)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new Dimensions(length, breadth, height));

    [Fact]
    public void Constructor_accepts_the_smallest_and_largest_allowed_sides()
    {
        var smallest = new Dimensions(1, 1, 1);
        var largest = new Dimensions(Dimensions.MaxSideMm, Dimensions.MaxSideMm, Dimensions.MaxSideMm);

        Assert.Equal(1, smallest.LengthMm);
        Assert.Equal(Dimensions.MaxSideMm, largest.HeightMm);
    }

    [Fact]
    public void TryCreate_returns_false_instead_of_throwing_for_invalid_input()
    {
        var created = Dimensions.TryCreate(200, 0, 150, out var dimensions);

        Assert.False(created);
        Assert.Null(dimensions);
    }

    [Fact]
    public void TryCreate_returns_the_value_for_valid_input()
    {
        var created = Dimensions.TryCreate(200, 300, 150, out var dimensions);

        Assert.True(created);
        Assert.Equal(new Dimensions(200, 300, 150), dimensions);
    }

    [Fact]
    public void Volume_multiplies_the_three_sides_without_overflowing()
    {
        var dimensions = new Dimensions(10_000, 10_000, 10_000);

        Assert.Equal(1_000_000_000_000L, dimensions.VolumeMm3);
    }

    [Fact]
    public void A_package_of_exactly_the_limit_fits()
    {
        var package = new Dimensions(200, 300, 150);

        Assert.True(package.FitsWithin(new Dimensions(200, 300, 150)));
    }

    [Theory]
    [InlineData(201, 300, 150)]
    [InlineData(200, 301, 150)]
    [InlineData(200, 300, 151)]
    public void A_package_one_millimetre_over_on_any_side_does_not_fit(int length, int breadth, int height)
    {
        var package = new Dimensions(length, breadth, height);

        Assert.False(package.FitsWithin(new Dimensions(200, 300, 150)));
    }

    [Fact]
    public void A_package_fits_when_it_can_be_turned_to_suit_the_box()
    {
        // Same box, different order of sides.
        var package = new Dimensions(300, 150, 200);

        Assert.True(package.FitsWithin(new Dimensions(200, 300, 150), allowRotation: true));
    }

    [Fact]
    public void Rotation_can_be_switched_off_so_sides_are_compared_as_supplied()
    {
        var package = new Dimensions(300, 150, 200);

        Assert.False(package.FitsWithin(new Dimensions(200, 300, 150), allowRotation: false));
    }

    [Fact]
    public void Fitting_against_a_null_limit_is_a_programming_error()
    {
        var package = new Dimensions(200, 300, 150);

        Assert.Throws<ArgumentNullException>(() => package.FitsWithin(null!));
    }

    [Fact]
    public void Dimensions_compare_by_value()
    {
        Assert.Equal(new Dimensions(200, 300, 150), new Dimensions(200, 300, 150));
        Assert.NotEqual(new Dimensions(200, 300, 150), new Dimensions(150, 300, 200));
    }

    [Fact]
    public void ToString_reads_as_length_by_breadth_by_height()
        => Assert.Equal("200x300x150mm", new Dimensions(200, 300, 150).ToString());
}
