using RoomBooking.Services.Contracts;
using RoomBooking.Services.Models;

namespace RoomBooking.Services.Repositories;

public interface IReservationRepository
{
    Task<PagedResult<ReservationModel>> GetForRoomAsync(
        ReservationQuery query,
        CancellationToken cancellationToken = default);

    Task<bool> HasOverlapAsync(
        Guid roomId,
        DateTimeOffset requestedStartUtc,
        DateTimeOffset requestedEndUtc,
        CancellationToken cancellationToken = default);

    Task<ReservationModel> AddAsync(
        CreateReservationCommand command,
        CancellationToken cancellationToken = default);
}