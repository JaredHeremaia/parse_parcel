namespace Shipping.Core.Model;

/// <summary>
/// A partial change to a package type. Null means "leave this as it is", so a caller can
/// change one field without knowing the others. Merging happens in <see cref="PackageCatalog"/>;
/// the entity itself still takes a complete, valid set of values.
/// </summary>
public sealed record PackageTypeChanges(
    string? Name = null,
    int? LengthMm = null,
    int? BreadthMm = null,
    int? HeightMm = null,
    decimal? Cost = null)
{
    public bool IsEmpty =>
        Name is null && LengthMm is null && BreadthMm is null && HeightMm is null && Cost is null;

    /// <summary>The dimensions this change produces when applied to <paramref name="current"/>.</summary>
    public bool TryApplyTo(Dimensions current, out Dimensions? dimensions)
    {
        ArgumentNullException.ThrowIfNull(current);

        return Dimensions.TryCreate(
            LengthMm ?? current.LengthMm,
            BreadthMm ?? current.BreadthMm,
            HeightMm ?? current.HeightMm,
            out dimensions);
    }
}
