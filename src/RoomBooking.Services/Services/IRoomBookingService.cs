using RoomBooking.Services.Contracts;

namespace RoomBooking.Services.Services;

public interface IRoomBookingService
{
    Task<GetRoomsResult> GetRoomsAsync(
        DateTimeOffset? start,
        DateTimeOffset? end,
        CancellationToken cancellationToken = default);

    Task<GetReservationsResult> GetReservationsAsync(
        Guid roomId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<CreateReservationResult> CreateReservationAsync(
        Guid roomId,
        DateTimeOffset start,
        DateTimeOffset end,
        string title,
        CancellationToken cancellationToken = default);
}