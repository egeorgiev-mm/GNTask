using RoomBooking.Api.Contracts;
using RoomBooking.Api.Infrastructure;
using RoomBooking.Api.Mappings;
using RoomBooking.Services.Contracts;
using RoomBooking.Services.Services;

namespace RoomBooking.Api.Endpoints;

public static class RoomEndpoints
{
    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/rooms");

        group.MapGet(string.Empty, GetRoomsAsync)
            .AddEndpointFilter(new ConditionalGetFilter(ConditionalGetScope.Rooms))
            .WithName("GetRooms")
            .WithSummary("Get rooms with optional availability filtering.")
            .Produces<IReadOnlyList<RoomResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status304NotModified);

        return app;
    }

    private static async Task<IResult> GetRoomsAsync(
        DateTimeOffset? start,
        DateTimeOffset? end,
        IRoomBookingService roomBookingService,
        CancellationToken cancellationToken)
    {
        var result = await roomBookingService.GetRoomsAsync(start, end, cancellationToken);
        return result switch
        {
            GetRoomsResult.Success success => TypedResults.Ok<IReadOnlyList<RoomResponse>>(success.Rooms.Select(room => room.ToResponse()).ToArray()),
            GetRoomsResult.ValidationFailed validation => ProblemDetailsMapping.ValidationFailed(validation.Failure),
            _ => ProblemDetailsMapping.UnexpectedError()
        };
    }
}
