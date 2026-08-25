namespace Shipping.Core.Model;

/// <summary>
/// A box we sell: a name ("Small"), the largest package it will hold, and a flat cost.
/// Costs are plain decimals - the service quotes in NZD and does not carry a currency.
/// </summary>
public sealed class PackageType
{
    public const int MaxNameLength = 50;

    /// <summary>Highest cost we will accept for a package type, a guard against fat-finger input.</summary>
    public const decimal MaxCost = 100_000m;

    public PackageType(Guid id, string name, Dimensions maxDimensions, decimal cost)
    {
        ArgumentNullException.ThrowIfNull(maxDimensions);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id must not be empty.", nameof(id));
        }

        Id = id;
        Name = NormaliseName(name, nameof(name));
        Cost = RequireCost(cost, nameof(cost));
        LengthMm = maxDimensions.LengthMm;
        BreadthMm = maxDimensions.BreadthMm;
        HeightMm = maxDimensions.HeightMm;
    }

    /// <summary>Required by EF Core for materialisation.</summary>
    private PackageType() => Name = string.Empty;

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public decimal Cost { get; private set; }

    // Stored as three columns so persistence stays trivial; exposed as a value object.
    public int LengthMm { get; private set; }

    public int BreadthMm { get; private set; }

    public int HeightMm { get; private set; }

    public Dimensions MaxDimensions => new(LengthMm, BreadthMm, HeightMm);

    public long MaxVolumeMm3 => MaxDimensions.VolumeMm3;

    public void Update(string name, Dimensions maxDimensions, decimal cost)
    {
        ArgumentNullException.ThrowIfNull(maxDimensions);

        Name = NormaliseName(name, nameof(name));
        Cost = RequireCost(cost, nameof(cost));
        LengthMm = maxDimensions.LengthMm;
        BreadthMm = maxDimensions.BreadthMm;
        HeightMm = maxDimensions.HeightMm;
    }

    public override string ToString() => $"{Name} ({MaxDimensions}) {Cost:0.00}";

    private static string NormaliseName(string name, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", parameterName);
        }

        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Name must be {MaxNameLength} characters or fewer.",
                parameterName);
        }

        return trimmed;
    }

    private static decimal RequireCost(decimal cost, string parameterName)
    {
        if (cost < 0m || cost > MaxCost)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                cost,
                $"Cost must be between 0 and {MaxCost:0.00}.");
        }

        return decimal.Round(cost, 2, MidpointRounding.AwayFromZero);
    }
}
