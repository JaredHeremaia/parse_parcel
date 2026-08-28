using Shipping.Core.Model;
using Shipping.Core.Storage;
using Xunit;

namespace Shipping.Core.Tests;

public class PackageCatalogTests
{
    private static PackageCatalog CreateCatalog(IEnumerable<PackageType>? seed = null)
        => new(new InMemoryPackageTypeStore(seed));

    [Fact]
    public async Task Listing_returns_the_price_list_cheapest_first()
    {
        var packageTypes = await CreateCatalog().ListAsync();

        Assert.Equal(
            new[] { "Small", "Medium", "Large" },
            packageTypes.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task A_package_type_can_be_fetched_by_id()
    {
        var result = await CreateCatalog().GetAsync(StandardPackageTypes.MediumId.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal("Medium", result.Value.Name);
    }

    [Theory]
    [InlineData("Small")]
    [InlineData("small")]
    [InlineData("  SMALL  ")]
    public async Task A_package_type_can_be_fetched_by_name_whatever_the_casing(string key)
    {
        var result = await CreateCatalog().GetAsync(key);

        Assert.True(result.IsSuccess);
        Assert.Equal("Small", result.Value.Name);
    }

    [Fact]
    public async Task Fetching_something_that_does_not_exist_reports_not_found()
    {
        var result = await CreateCatalog().GetAsync("enormous");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public async Task Fetching_an_unknown_id_reports_not_found()
    {
        var result = await CreateCatalog().GetAsync(Guid.NewGuid().ToString());

        Assert.Equal(ErrorCode.NotFound, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Fetching_with_no_key_is_invalid(string key)
    {
        var result = await CreateCatalog().GetAsync(key);

        Assert.Equal(ErrorCode.Invalid, result.Error);
    }

    [Fact]
    public async Task A_new_package_type_can_be_added_and_read_back()
    {
        var catalog = CreateCatalog();

        var created = await catalog.CreateAsync("Extra Large", new Dimensions(500, 700, 300), 12.50m);

        Assert.True(created.IsSuccess);
        Assert.NotEqual(Guid.Empty, created.Value.Id);

        var fetched = await catalog.GetAsync("extra large");

        Assert.True(fetched.IsSuccess);
        Assert.Equal(created.Value.Id, fetched.Value.Id);
        Assert.Equal(12.50m, fetched.Value.Cost);
    }

    [Theory]
    [InlineData("Small")]
    [InlineData("small")]
    public async Task Adding_a_duplicate_name_is_a_conflict(string name)
    {
        var result = await CreateCatalog().CreateAsync(name, new Dimensions(10, 10, 10), 1m);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Adding_without_a_name_is_invalid(string name)
    {
        var result = await CreateCatalog().CreateAsync(name, new Dimensions(10, 10, 10), 1m);

        Assert.Equal(ErrorCode.Invalid, result.Error);
    }

    [Fact]
    public async Task Adding_with_an_over_long_name_is_invalid()
    {
        var result = await CreateCatalog()
            .CreateAsync(new string('x', PackageType.MaxNameLength + 1), new Dimensions(10, 10, 10), 1m);

        Assert.Equal(ErrorCode.Invalid, result.Error);
    }

    [Fact]
    public async Task Adding_with_a_negative_cost_is_invalid_rather_than_an_exception()
    {
        var result = await CreateCatalog().CreateAsync("Cheap", new Dimensions(10, 10, 10), -1m);

        Assert.Equal(ErrorCode.Invalid, result.Error);
    }

    [Fact]
    public async Task An_added_package_type_appears_in_the_list()
    {
        var catalog = CreateCatalog();

        await catalog.CreateAsync("Extra Large", new Dimensions(500, 700, 300), 12.50m);
        var packageTypes = await catalog.ListAsync();

        Assert.Equal(4, packageTypes.Count);
    }

    [Fact]
    public async Task Updating_applies_every_field_it_is_given()
    {
        var catalog = CreateCatalog();

        var result = await catalog.UpdateAsync(
            StandardPackageTypes.SmallId,
            new PackageTypeChanges("Small", 210, 310, 160, 5.50m));

        Assert.True(result.IsSuccess);

        var fetched = await catalog.GetAsync("small");

        Assert.Equal(new Dimensions(210, 310, 160), fetched.Value.MaxDimensions);
        Assert.Equal(5.50m, fetched.Value.Cost);
    }

    [Fact]
    public async Task Fields_left_out_keep_their_current_value()
    {
        var catalog = CreateCatalog();

        var result = await catalog.UpdateAsync(
            StandardPackageTypes.SmallId,
            new PackageTypeChanges(Cost: 6.25m));

        Assert.True(result.IsSuccess);
        Assert.Equal(6.25m, result.Value.Cost);

        // Everything else is untouched.
        Assert.Equal("Small", result.Value.Name);
        Assert.Equal(new Dimensions(200, 300, 150), result.Value.MaxDimensions);
    }

    [Fact]
    public async Task A_single_side_can_be_changed_on_its_own()
    {
        var result = await CreateCatalog().UpdateAsync(
            StandardPackageTypes.SmallId,
            new PackageTypeChanges(LengthMm: 250));

        Assert.True(result.IsSuccess);
        Assert.Equal(new Dimensions(250, 300, 150), result.Value.MaxDimensions);
        Assert.Equal(5m, result.Value.Cost);
    }

    [Fact]
    public async Task An_empty_change_leaves_the_package_type_as_it_was()
    {
        var result = await CreateCatalog()
            .UpdateAsync(StandardPackageTypes.SmallId, new PackageTypeChanges());

        Assert.True(result.IsSuccess);
        Assert.Equal("Small", result.Value.Name);
        Assert.Equal(new Dimensions(200, 300, 150), result.Value.MaxDimensions);
        Assert.Equal(5m, result.Value.Cost);
    }

    [Fact]
    public async Task Updating_something_that_does_not_exist_reports_not_found()
    {
        var result = await CreateCatalog()
            .UpdateAsync(Guid.NewGuid(), new PackageTypeChanges(Name: "Small"));

        Assert.Equal(ErrorCode.NotFound, result.Error);
    }

    [Fact]
    public async Task Renaming_onto_another_package_types_name_is_a_conflict()
    {
        var result = await CreateCatalog()
            .UpdateAsync(StandardPackageTypes.SmallId, new PackageTypeChanges(Name: "medium"));

        Assert.Equal(ErrorCode.Conflict, result.Error);
    }

    [Fact]
    public async Task Keeping_its_own_name_is_not_a_conflict()
    {
        var result = await CreateCatalog()
            .UpdateAsync(StandardPackageTypes.SmallId, new PackageTypeChanges(Name: "SMALL"));

        Assert.True(result.IsSuccess);
        Assert.Equal("SMALL", result.Value.Name);
    }

    [Fact]
    public async Task Updating_with_an_invalid_name_is_invalid()
    {
        var result = await CreateCatalog()
            .UpdateAsync(StandardPackageTypes.SmallId, new PackageTypeChanges(Name: " "));

        Assert.Equal(ErrorCode.Invalid, result.Error);
    }

    [Fact]
    public async Task A_rejected_update_leaves_the_package_type_untouched()
    {
        var store = new InMemoryPackageTypeStore();
        var catalog = new PackageCatalog(store);

        // The cost is over PackageType.MaxCost, so Update throws part way through. The
        // store hands out live references, so a half-applied change would be visible.
        var result = await catalog.UpdateAsync(
            StandardPackageTypes.SmallId,
            new PackageTypeChanges(Name: "Huge", Cost: PackageType.MaxCost + 1m));

        Assert.Equal(ErrorCode.Invalid, result.Error);

        var after = await store.GetByIdAsync(StandardPackageTypes.SmallId);

        Assert.Equal("Small", after!.Name);
        Assert.Equal(5m, after.Cost);
        Assert.Equal(new Dimensions(200, 300, 150), after.MaxDimensions);
    }

    [Fact]
    public async Task A_rejected_update_does_not_change_the_dimensions_either()
    {
        var store = new InMemoryPackageTypeStore();
        var catalog = new PackageCatalog(store);

        var result = await catalog.UpdateAsync(
            StandardPackageTypes.SmallId,
            new PackageTypeChanges(Name: " ", LengthMm: 250));

        Assert.Equal(ErrorCode.Invalid, result.Error);

        var after = await store.GetByIdAsync(StandardPackageTypes.SmallId);

        Assert.Equal(200, after!.MaxDimensions.LengthMm);
    }

    [Fact]
    public async Task Updating_to_an_impossible_side_is_invalid()
    {
        var result = await CreateCatalog()
            .UpdateAsync(StandardPackageTypes.SmallId, new PackageTypeChanges(LengthMm: 0));

        Assert.Equal(ErrorCode.Invalid, result.Error);
    }

    [Fact]
    public async Task Deleting_removes_the_package_type()
    {
        var catalog = CreateCatalog();

        var deleted = await catalog.DeleteAsync(StandardPackageTypes.LargeId);

        Assert.True(deleted.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, (await catalog.GetAsync("large")).Error);
        Assert.Equal(2, (await catalog.ListAsync()).Count);
    }

    [Fact]
    public async Task Deleting_twice_reports_not_found_the_second_time()
    {
        var catalog = CreateCatalog();

        await catalog.DeleteAsync(StandardPackageTypes.LargeId);
        var second = await catalog.DeleteAsync(StandardPackageTypes.LargeId);

        Assert.False(second.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, second.Error);
    }

    [Fact]
    public void A_store_is_required()
        => Assert.Throws<ArgumentNullException>(() => new PackageCatalog(null!));
}
