using Microsoft.AspNetCore.Http.HttpResults;
using RoomBooking.Services.Contracts;

namespace RoomBooking.Api.Infrastructure;

public static class ProblemDetailsMapping
{
    public static IResult ValidationFailed(ValidationFailure failure)
    {
        var errors = failure.ErrorsByField.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToArray(),
            StringComparer.Ordinal);

        return TypedResults.ValidationProblem(
            errors,
            detail: failure.Message,
            title: "Validation failed",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = failure.Code
            });
    }

    public static ProblemHttpResult RoomNotFound(NotFoundFailure failure)
    {
        return TypedResults.Problem(
            detail: failure.Message,
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = failure.Code
            });
    }

    public static ProblemHttpResult ReservationConflict(ConflictFailure failure)
    {
        return TypedResults.Problem(
            detail: failure.Message,
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = failure.Code
            });
    }

    public static ProblemHttpResult BindingProblem(string detail)
    {
        return TypedResults.Problem(
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "bad_request"
            });
    }

    public static ProblemHttpResult UnexpectedError()
    {
        return TypedResults.Problem(
            detail: "An unexpected error occurred.",
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "internal_error"
            });
    }
}
