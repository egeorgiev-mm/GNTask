using RoomBooking.Services.Contracts;

namespace RoomBooking.Services.Services;

public interface IReservationVersionTokenService
{
    Task<GetRoomsVersionTokenResult> GetRoomsTokenAsync(
        DateTimeOffset? start,
        DateTimeOffset? end,
        CancellationToken cancellationToken = default);

    Task<GetRoomReservationsVersionTokenResult> GetRoomReservationsTokenAsync(
        Guid roomId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);
}