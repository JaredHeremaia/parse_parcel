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
/// Body for POST /api/packages and PUT /api/packages/{id}. Fields are nullable so a
/// missing value is reported as "required" rather than silently defaulting to zero.
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
        switch (value)
        {
            case null:
                errors.Add($"{field} is required.");
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
