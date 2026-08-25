namespace Shipping.Core.Model;

/// <summary>
/// The published price list. Ids are fixed so seeded data is stable across
/// restarts and between the in-memory store and Postgres.
/// </summary>
public static class StandardPackageTypes
{
    public static readonly Guid SmallId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid MediumId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid LargeId = new("33333333-3333-3333-3333-333333333333");

    public static IReadOnlyList<PackageType> Create() =>
    [
        new PackageType(SmallId, "Small", new Dimensions(200, 300, 150), 5.00m),
        new PackageType(MediumId, "Medium", new Dimensions(300, 400, 200), 7.50m),
        new PackageType(LargeId, "Large", new Dimensions(400, 600, 250), 8.50m),
    ];
}
