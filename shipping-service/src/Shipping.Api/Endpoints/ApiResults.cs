using Shipping.Core;

namespace Shipping.Api.Endpoints;

/// <summary>
/// Single place where domain failures become HTTP responses, so every endpoint
/// reports problems the same way (RFC 7807 problem details).
/// </summary>
internal static class ApiResults
{
    public static IResult Invalid(IReadOnlyList<string> errors)
        => Results.Problem(
            title: "Invalid request",
            detail: string.Join(" ", errors),
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?> { ["errors"] = errors });

    public static IResult Invalid(string message)
        => Invalid([message]);

    /// <summary>Maps an expected domain failure onto the matching status code.</summary>
    public static IResult Failure(ErrorCode error, string? message)
    {
        var detail = message ?? "The request could not be completed.";

        return error switch
        {
            ErrorCode.NotFound => Results.Problem(
                title: "Not found",
                detail: detail,
                statusCode: StatusCodes.Status404NotFound),
            ErrorCode.Conflict => Results.Problem(
                title: "Conflict",
                detail: detail,
                statusCode: StatusCodes.Status409Conflict),
            ErrorCode.Invalid => Invalid(detail),
            _ => Results.Problem(
                title: "Unexpected error",
                detail: detail,
                statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}
