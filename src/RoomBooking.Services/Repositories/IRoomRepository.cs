using RoomBooking.Services.Models;

namespace RoomBooking.Services.Repositories;

public interface IRoomRepository
{
    Task<IReadOnlyList<RoomModel>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomModel>> GetAvailableAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid roomId, CancellationToken cancellationToken = default);
}