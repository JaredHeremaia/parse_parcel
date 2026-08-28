using Shipping.Contracts;
using Shipping.Core;
using Shipping.Core.Model;

namespace Shipping.Api.Endpoints;

internal static class PackageEndpoints
{
    public static RouteGroupBuilder MapPackageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/packages").WithTags("Packages");

        group.MapGet("/", GetAllAsync).WithName("GetPackages");
        group.MapGet("/{idOrName}", GetOneAsync).WithName("GetPackage");
        group.MapPost("/", CreateAsync).WithName("CreatePackage");
        group.MapPatch("/{id:guid}", UpdateAsync).WithName("UpdatePackage");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeletePackage");

        return group;
    }

    /// <summary>GET /api/packages - every package type with its size, dimensions and price.</summary>
    private static async Task<IResult> GetAllAsync(
        PackageCatalog catalog,
        PackagingOptions options,
        CancellationToken cancellationToken)
    {
        var packageTypes = await catalog.ListAsync(cancellationToken);

        return TypedResults.Ok(
            packageTypes.Select(p => p.ToResponse(options.MaxWeightKg)).ToList());
    }

    /// <summary>GET /api/packages/{idOrName} - one package type, by id or by name.</summary>
    private static async Task<IResult> GetOneAsync(
        string idOrName,
        PackageCatalog catalog,
        PackagingOptions options,
        CancellationToken cancellationToken)
    {
        var result = await catalog.GetAsync(idOrName, cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value.ToResponse(options.MaxWeightKg))
            : ApiResults.Failure(result.Error, result.Message);
    }

    /// <summary>POST /api/packages - add a package type.</summary>
    private static async Task<IResult> CreateAsync(
        PackageTypeRequest request,
        PackageCatalog catalog,
        PackagingOptions options,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiResults.Invalid("A request body is required.");
        }

        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return ApiResults.Invalid(errors);
        }

        var dimensions = new Dimensions(request.LengthMm!.Value, request.BreadthMm!.Value, request.HeightMm!.Value);

        var result = await catalog.CreateAsync(request.Name!, dimensions, request.Cost!.Value, cancellationToken);

        if (!result.IsSuccess)
        {
            return ApiResults.Failure(result.Error, result.Message);
        }

        var created = result.Value;
        return TypedResults.Created(
            $"/api/packages/{created.Id}",
            created.ToResponse(options.MaxWeightKg));
    }

    /// <summary>
    /// PATCH /api/packages/{id} - change part of a package type. Fields left out of the
    /// body keep their current value.
    /// </summary>
    private static async Task<IResult> UpdateAsync(
        Guid id,
        PackageTypePatchRequest request,
        PackageCatalog catalog,
        PackagingOptions options,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ApiResults.Invalid("A request body is required.");
        }

        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return ApiResults.Invalid(errors);
        }

        var changes = new PackageTypeChanges(
            request.Name,
            request.LengthMm,
            request.BreadthMm,
            request.HeightMm,
            request.Cost);

        var result = await catalog.UpdateAsync(id, changes, cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value.ToResponse(options.MaxWeightKg))
            : ApiResults.Failure(result.Error, result.Message);
    }

    /// <summary>DELETE /api/packages/{id} - remove a package type.</summary>
    private static async Task<IResult> DeleteAsync(
        Guid id,
        PackageCatalog catalog,
        CancellationToken cancellationToken)
    {
        var result = await catalog.DeleteAsync(id, cancellationToken);

        return result.IsSuccess
            ? TypedResults.NoContent()
            : ApiResults.Failure(result.Error, result.Message);
    }
}
