namespace Shipping.Core.Model;

/// <summary>
/// Tunable packaging rules. A plain POCO so the domain stays free of framework
/// types; the API binds it from the "Packaging" configuration section.
/// </summary>
public sealed class PackagingOptions
{
    public const decimal DefaultMaxWeightKg = 25m;

    /// <summary>We currently cannot move heavy packages, so anything above this is refused.</summary>
    public decimal MaxWeightKg { get; set; } = DefaultMaxWeightKg;

    /// <summary>Whether a package may be turned to fit a box. See <see cref="Dimensions.FitsWithin"/>.</summary>
    public bool AllowRotation { get; set; } = true;

    public void Validate()
    {
        if (MaxWeightKg <= 0m)
        {
            throw new InvalidOperationException("Packaging:MaxWeightKg must be greater than zero.");
        }
    }
}
