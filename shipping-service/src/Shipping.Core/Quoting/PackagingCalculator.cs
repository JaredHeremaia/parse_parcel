using Shipping.Core.Model;
using Shipping.Core.Storage;

namespace Shipping.Core.Quoting;

/// <summary>Advises on the cost and type of package required for a shipment.</summary>
public interface IPackagingCalculator
{
    Task<QuoteResult> QuoteAsync(
        Dimensions dimensions,
        decimal weightKg,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IPackagingCalculator"/>
public sealed class PackagingCalculator : IPackagingCalculator
{
    private readonly IPackageTypeStore _store;
    private readonly PackagingOptions _options;

    public PackagingCalculator(IPackageTypeStore store, PackagingOptions? options = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? new PackagingOptions();
        _options.Validate();
    }

    public async Task<QuoteResult> QuoteAsync(
        Dimensions dimensions,
        decimal weightKg,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dimensions);

        if (weightKg <= 0m)
        {
            return QuoteResult.Rejected(
                QuoteRejectionReason.InvalidInput,
                "Weight must be greater than zero.");
        }

        if (weightKg > _options.MaxWeightKg)
        {
            return QuoteResult.Rejected(
                QuoteRejectionReason.Overweight,
                $"We cannot currently ship packages over {_options.MaxWeightKg:0.##}kg " +
                $"(this one is {weightKg:0.##}kg).");
        }

        var packageTypes = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Cheapest box that holds it. Volume then name break ties so the result is
        // deterministic when two package types cost the same.
        var match = packageTypes
            .Where(type => dimensions.FitsWithin(type.MaxDimensions, _options.AllowRotation))
            .OrderBy(type => type.Cost)
            .ThenBy(type => type.MaxVolumeMm3)
            .ThenBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (match is null)
        {
            return QuoteResult.Rejected(
                QuoteRejectionReason.Oversized,
                $"No package type can hold a package measuring {dimensions}.");
        }

        return QuoteResult.Quoted(
            new PackagingQuote(match.Id, match.Name, match.Cost, dimensions, weightKg));
    }
}
