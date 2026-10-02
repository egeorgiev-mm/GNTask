using Microsoft.AspNetCore.Http.HttpResults;
using RoomBooking.Services.Contracts;
using RoomBooking.Services.Services;

namespace RoomBooking.Api.Infrastructure;

public enum ConditionalGetScope
{
    Rooms,
    RoomReservations
}

public sealed class ConditionalGetFilter(ConditionalGetScope scope) : IEndpointFilter
{
    private const string CacheControlValue = "no-cache";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var tokenService = context.HttpContext.RequestServices.GetRequiredService<IReservationVersionTokenService>();
        var cancellationToken = context.HttpContext.RequestAborted;

        var tokenResult = scope switch
        {
            ConditionalGetScope.Rooms => await GetRoomsTokenAsync(context, tokenService, cancellationToken),
            ConditionalGetScope.RoomReservations => await GetRoomReservationsTokenAsync(context, tokenService, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported conditional GET scope.")
        };

        if (tokenResult.ShortCircuitResult is not null)
        {
            return tokenResult.ShortCircuitResult;
        }

        var token = tokenResult.Token!;
        var weakEtag = WeakETag.Format(token);
        var ifNoneMatch = context.HttpContext.Request.Headers.IfNoneMatch.ToString();

        if (WeakETag.Matches(ifNoneMatch, token))
        {
            SetCachingHeaders(context.HttpContext.Response, weakEtag);
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        var result = await next(context);
        if (result is IStatusCodeHttpResult { StatusCode: StatusCodes.Status200OK })
        {
            SetCachingHeaders(context.HttpContext.Response, weakEtag);
        }

        return result;
    }

    private static async Task<TokenEvaluationResult> GetRoomsTokenAsync(
        EndpointFilterInvocationContext context,
        IReservationVersionTokenService tokenService,
        CancellationToken cancellationToken)
    {
        var start = context.GetArgument<DateTimeOffset?>(0);
        var end = context.GetArgument<DateTimeOffset?>(1);

        var tokenResult = await tokenService.GetRoomsTokenAsync(start, end, cancellationToken);
        return tokenResult switch
        {
            GetRoomsVersionTokenResult.Success success => TokenEvaluationResult.Success(success.Token),
            GetRoomsVersionTokenResult.ValidationFailed validation => TokenEvaluationResult.Failed(ProblemDetailsMapping.ValidationFailed(validation.Failure)),
            _ => TokenEvaluationResult.Failed(ProblemDetailsMapping.UnexpectedError())
        };
    }

    private static async Task<TokenEvaluationResult> GetRoomReservationsTokenAsync(
        EndpointFilterInvocationContext context,
        IReservationVersionTokenService tokenService,
        CancellationToken cancellationToken)
    {
        var roomId = context.GetArgument<Guid>(0);
        var start = context.GetArgument<DateTimeOffset?>(1);
        var end = context.GetArgument<DateTimeOffset?>(2);
        var limit = context.GetArgument<int?>(3) ?? 50;
        var offset = context.GetArgument<int?>(4) ?? 0;

        var tokenResult = await tokenService.GetRoomReservationsTokenAsync(roomId, start, end, limit, offset, cancellationToken);
        return tokenResult switch
        {
            GetRoomReservationsVersionTokenResult.Success success => TokenEvaluationResult.Success(success.Token),
            GetRoomReservationsVersionTokenResult.ValidationFailed validation => TokenEvaluationResult.Failed(ProblemDetailsMapping.ValidationFailed(validation.Failure)),
            GetRoomReservationsVersionTokenResult.NotFound notFound => TokenEvaluationResult.Failed(ProblemDetailsMapping.RoomNotFound(notFound.Failure)),
            _ => TokenEvaluationResult.Failed(ProblemDetailsMapping.UnexpectedError())
        };
    }

    private static void SetCachingHeaders(HttpResponse response, string weakEtag)
    {
        response.Headers.ETag = weakEtag;
        response.Headers.CacheControl = CacheControlValue;
    }

    private sealed record TokenEvaluationResult(string? Token, IResult? ShortCircuitResult)
    {
        public static TokenEvaluationResult Success(string token) => new(token, null);

        public static TokenEvaluationResult Failed(IResult result) => new(null, result);
    }
}
