using Shipping.Contracts;
using Shipping.Core.Model;
using Shipping.Core.Quoting;

namespace Shipping.Api.Endpoints;

internal static class QuoteEndpoints
{
    public static RouteGroupBuilder MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quotes").WithTags("Quotes");

        group.MapPost("/", QuoteAsync).WithName("CreateQuote");

        return group;
    }

    /// <summary>
    /// POST /api/quotes - given dimensions and weight, advise on cost and package type.
    /// Returns 422 when no packaging solution exists (too big or too heavy); the
    /// request was well formed, we simply cannot ship it.
    /// </summary>
    private static async Task<IResult> QuoteAsync(
        QuoteRequest request,
        IPackagingCalculator calculator,
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

        var result = await calculator.QuoteAsync(dimensions, request.WeightKg!.Value, cancellationToken);

        if (result.IsQuoted)
        {
            return TypedResults.Ok(result.Quote.ToResponse());
        }

        return result.RejectionReason == QuoteRejectionReason.InvalidInput
            ? ApiResults.Invalid(result.Message)
            : Results.Problem(
                title: "No packaging solution",
                detail: result.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                extensions: new Dictionary<string, object?>
                {
                    ["reason"] = result.RejectionReason?.ToString(),
                });
    }
}
