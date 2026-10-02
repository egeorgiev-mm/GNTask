using RoomBooking.Api.Contracts;
using RoomBooking.Api.Infrastructure;
using RoomBooking.Api.Mappings;
using RoomBooking.Services.Contracts;
using RoomBooking.Services.Services;

namespace RoomBooking.Api.Endpoints;

public static class ReservationEndpoints
{
    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/rooms/{roomId:guid}/reservations");

        group.MapGet(string.Empty, GetReservationsAsync)
            .AddEndpointFilter(new ConditionalGetFilter(ConditionalGetScope.RoomReservations))
            .WithName("GetRoomReservations")
            .WithSummary("Get room reservations in an optional date window.")
            .Produces<PagedResponse<ReservationResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status304NotModified);

        group.MapPost(string.Empty, CreateReservationAsync)
            .WithName("CreateRoomReservation")
            .WithSummary("Create a reservation for a room.")
            .Produces<ReservationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> GetReservationsAsync(
        Guid roomId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int? limit,
        int? offset,
        IRoomBookingService roomBookingService,
        CancellationToken cancellationToken)
    {
        var normalizedLimit = limit ?? 50;
        var normalizedOffset = offset ?? 0;

        var result = await roomBookingService.GetReservationsAsync(
            roomId,
            start,
            end,
            normalizedLimit,
            normalizedOffset,
            cancellationToken);

        return result switch
        {
            GetReservationsResult.Success success => TypedResults.Ok(success.Reservations.ToResponse(item => item.ToResponse())),
            GetReservationsResult.ValidationFailed validation => ProblemDetailsMapping.ValidationFailed(validation.Failure),
            GetReservationsResult.NotFound notFound => ProblemDetailsMapping.RoomNotFound(notFound.Failure),
            _ => ProblemDetailsMapping.UnexpectedError()
        };
    }

    private static async Task<IResult> CreateReservationAsync(
        Guid roomId,
        CreateReservationRequest request,
        IRoomBookingService roomBookingService,
        CancellationToken cancellationToken)
    {
        if (request.Start is null)
        {
            return ProblemDetailsMapping.ValidationFailed(new ValidationFailure(
                "start_required",
                "Start is required.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["start"] = ["Start is required."]
                }));
        }

        if (request.End is null)
        {
            return ProblemDetailsMapping.ValidationFailed(new ValidationFailure(
                "end_required",
                "End is required.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["end"] = ["End is required."]
                }));
        }

        if (request.Title is null)
        {
            return ProblemDetailsMapping.ValidationFailed(new ValidationFailure(
                "title_required",
                "Title is required.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["title"] = ["Title is required."]
                }));
        }

        var result = await roomBookingService.CreateReservationAsync(
            roomId,
            request.Start.Value,
            request.End.Value,
            request.Title,
            cancellationToken);

        return result switch
        {
            CreateReservationResult.Success success => TypedResults.Json(
                success.Reservation.ToResponse(),
                statusCode: StatusCodes.Status201Created),
            CreateReservationResult.ValidationFailed validation => ProblemDetailsMapping.ValidationFailed(validation.Failure),
            CreateReservationResult.NotFound notFound => ProblemDetailsMapping.RoomNotFound(notFound.Failure),
            CreateReservationResult.Conflict conflict => ProblemDetailsMapping.ReservationConflict(conflict.Failure),
            _ => ProblemDetailsMapping.UnexpectedError()
        };
    }
}
