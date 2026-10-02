namespace RoomBooking.Services.Repositories;

public interface IReservationVersionRepository
{
    Task<long> GetGlobalVersionAsync(CancellationToken cancellationToken = default);

    Task<long?> GetRoomVersionAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task IncrementGlobalAndRoomVersionAsync(
        Guid roomId,
        CancellationToken cancellationToken = default);
}