namespace Shipping.Contracts;

/// <summary>Dimensions as they appear on the wire, in whole millimetres.</summary>
public sealed record DimensionsResponse(
    int LengthMm,
    int BreadthMm,
    int HeightMm,
    long VolumeMm3);

/// <summary>
/// A package type: its size name, dimensions and price. Costs are plain numbers -
/// the service quotes in NZD and does not return a currency.
/// </summary>
public sealed record PackageTypeResponse(
    Guid Id,
    string Name,
    DimensionsResponse Dimensions,
    decimal Cost,
    decimal MaxWeightKg);

/// <summary>
/// Body for POST /api/packages. Fields are nullable so a missing value is reported as
/// "required" rather than silently defaulting to zero.
/// </summary>
public sealed record PackageTypeRequest(
    string? Name,
    int? LengthMm,
    int? BreadthMm,
    int? HeightMm,
    decimal? Cost)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("name is required.");
        }

        ValidationRules.RequirePositiveSide(LengthMm, "lengthMm", errors);
        ValidationRules.RequirePositiveSide(BreadthMm, "breadthMm", errors);
        ValidationRules.RequirePositiveSide(HeightMm, "heightMm", errors);

        switch (Cost)
        {
            case null:
                errors.Add("cost is required.");
                break;
            case < 0m:
                errors.Add("cost must not be negative.");
                break;
        }

        return errors;
    }
}

/// <summary>
/// Body for PATCH /api/packages/{id}. Every field is optional: an absent field is left as
/// it is, so a caller can change one thing without restating the rest. Absent and null are
/// the same thing here, since none of these fields may legitimately be null.
/// </summary>
public sealed record PackageTypePatchRequest(
    string? Name = null,
    int? LengthMm = null,
    int? BreadthMm = null,
    int? HeightMm = null,
    decimal? Cost = null)
{
    public bool IsEmpty =>
        Name is null && LengthMm is null && BreadthMm is null && HeightMm is null && Cost is null;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (IsEmpty)
        {
            errors.Add("Provide at least one of name, lengthMm, breadthMm, heightMm or cost.");
            return errors;
        }

        // Only what was supplied is checked; the rest keeps its current, already valid value.
        if (Name is not null && string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("name must not be blank.");
        }

        ValidationRules.CheckSideIfSupplied(LengthMm, "lengthMm", errors);
        ValidationRules.CheckSideIfSupplied(BreadthMm, "breadthMm", errors);
        ValidationRules.CheckSideIfSupplied(HeightMm, "heightMm", errors);

        if (Cost is < 0m)
        {
            errors.Add("cost must not be negative.");
        }

        return errors;
    }
}

/// <summary>Body for POST /api/quotes: the package we are asked to advise on.</summary>
public sealed record QuoteRequest(
    int? LengthMm,
    int? BreadthMm,
    int? HeightMm,
    decimal? WeightKg)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        ValidationRules.RequirePositiveSide(LengthMm, "lengthMm", errors);
        ValidationRules.RequirePositiveSide(BreadthMm, "breadthMm", errors);
        ValidationRules.RequirePositiveSide(HeightMm, "heightMm", errors);

        switch (WeightKg)
        {
            case null:
                errors.Add("weightKg is required.");
                break;
            case <= 0m:
                errors.Add("weightKg must be greater than zero.");
                break;
        }

        return errors;
    }
}

/// <summary>A successful packaging solution.</summary>
public sealed record QuoteResponse(
    Guid PackageTypeId,
    string PackageType,
    decimal Cost,
    DimensionsResponse Dimensions,
    decimal WeightKg);

/// <summary>
/// RFC 7807 problem details as returned by the API, in the shape the CLI reads.
/// <see cref="Reason"/> is set on quote rejections (Overweight, Oversized, InvalidInput).
/// </summary>
public sealed record ApiErrorResponse(
    string? Title,
    string? Detail,
    int? Status,
    string? Reason,
    IReadOnlyList<string>? Errors);

internal static class ValidationRules
{
    /// <summary>Mirrors <c>Shipping.Core.Model.Dimensions</c> without taking a dependency on it.</summary>
    private const int MaxSideMm = 100_000;

    public static void RequirePositiveSide(int? value, string field, List<string> errors)
    {
        if (value is null)
        {
            errors.Add($"{field} is required.");
            return;
        }

        CheckSideIfSupplied(value, field, errors);
    }

    /// <summary>Checks a side only when one was given; absent is allowed on a patch.</summary>
    public static void CheckSideIfSupplied(int? value, string field, List<string> errors)
    {
        switch (value)
        {
            case null:
                break;
            case <= 0:
                errors.Add($"{field} must be greater than zero.");
                break;
            case > MaxSideMm:
                errors.Add($"{field} must not exceed {MaxSideMm}mm.");
                break;
        }
    }
}
