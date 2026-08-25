using Shipping.Core.Model;
using Xunit;

namespace Shipping.Core.Tests;

public class PackageTypeTests
{
    private static readonly Dimensions AnySize = new(200, 300, 150);

    [Fact]
    public void A_package_type_keeps_what_it_was_given()
    {
        var id = Guid.NewGuid();

        var packageType = new PackageType(id, "Small", AnySize, 5.00m);

        Assert.Equal(id, packageType.Id);
        Assert.Equal("Small", packageType.Name);
        Assert.Equal(AnySize, packageType.MaxDimensions);
        Assert.Equal(5.00m, packageType.Cost);
        Assert.Equal(9_000_000L, packageType.MaxVolumeMm3);
    }

    [Fact]
    public void Names_are_trimmed()
        => Assert.Equal("Small", new PackageType(Guid.NewGuid(), "  Small  ", AnySize, 5m).Name);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_name_is_required(string name)
        => Assert.Throws<ArgumentException>(() => new PackageType(Guid.NewGuid(), name, AnySize, 5m));

    [Fact]
    public void A_name_cannot_exceed_the_maximum_length()
    {
        var tooLong = new string('x', PackageType.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => new PackageType(Guid.NewGuid(), tooLong, AnySize, 5m));
    }

    [Fact]
    public void An_empty_id_is_rejected()
        => Assert.Throws<ArgumentException>(() => new PackageType(Guid.Empty, "Small", AnySize, 5m));

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-5)]
    public void A_negative_cost_is_rejected(decimal cost)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new PackageType(Guid.NewGuid(), "Small", AnySize, cost));

    [Fact]
    public void An_absurd_cost_is_rejected()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new PackageType(Guid.NewGuid(), "Small", AnySize, PackageType.MaxCost + 1m));

    [Fact]
    public void A_free_package_type_is_allowed()
        => Assert.Equal(0m, new PackageType(Guid.NewGuid(), "Free", AnySize, 0m).Cost);

    [Fact]
    public void Costs_are_rounded_to_cents()
        => Assert.Equal(5.13m, new PackageType(Guid.NewGuid(), "Small", AnySize, 5.125m).Cost);

    [Fact]
    public void Update_replaces_every_field_except_the_id()
    {
        var packageType = new PackageType(Guid.NewGuid(), "Small", AnySize, 5m);
        var id = packageType.Id;

        packageType.Update("Smaller", new Dimensions(100, 100, 100), 3.50m);

        Assert.Equal(id, packageType.Id);
        Assert.Equal("Smaller", packageType.Name);
        Assert.Equal(new Dimensions(100, 100, 100), packageType.MaxDimensions);
        Assert.Equal(3.50m, packageType.Cost);
    }

    [Fact]
    public void Update_applies_the_same_rules_as_creation()
    {
        var packageType = new PackageType(Guid.NewGuid(), "Small", AnySize, 5m);

        Assert.Throws<ArgumentException>(() => packageType.Update(" ", AnySize, 5m));
        Assert.Throws<ArgumentOutOfRangeException>(() => packageType.Update("Small", AnySize, -1m));
        Assert.Throws<ArgumentNullException>(() => packageType.Update("Small", null!, 5m));
    }

    [Fact]
    public void The_published_price_list_matches_the_service_description()
    {
        var priceList = StandardPackageTypes.Create();

        Assert.Collection(
            priceList,
            small =>
            {
                Assert.Equal("Small", small.Name);
                Assert.Equal(new Dimensions(200, 300, 150), small.MaxDimensions);
                Assert.Equal(5.00m, small.Cost);
            },
            medium =>
            {
                Assert.Equal("Medium", medium.Name);
                Assert.Equal(new Dimensions(300, 400, 200), medium.MaxDimensions);
                Assert.Equal(7.50m, medium.Cost);
            },
            large =>
            {
                Assert.Equal("Large", large.Name);
                Assert.Equal(new Dimensions(400, 600, 250), large.MaxDimensions);
                Assert.Equal(8.50m, large.Cost);
            });
    }
}
