using Shipping.Contracts;
using Shipping.Core.Model;
using Shipping.Core.Quoting;

namespace Shipping.Api.Endpoints;

/// <summary>Translates domain objects into wire contracts. Kept in one place so the
/// JSON shape cannot drift endpoint by endpoint.</summary>
internal static class ContractMapping
{
    public static DimensionsResponse ToResponse(this Dimensions dimensions)
        => new(dimensions.LengthMm, dimensions.BreadthMm, dimensions.HeightMm, dimensions.VolumeMm3);

    public static PackageTypeResponse ToResponse(this PackageType packageType, decimal maxWeightKg)
        => new(
            packageType.Id,
            packageType.Name,
            packageType.MaxDimensions.ToResponse(),
            packageType.Cost,
            maxWeightKg);

    public static QuoteResponse ToResponse(this PackagingQuote quote)
        => new(
            quote.PackageTypeId,
            quote.PackageTypeName,
            quote.Cost,
            quote.Dimensions.ToResponse(),
            quote.WeightKg);
}
