using RoomBooking.Services.Contracts;
using RoomBooking.Services.Repositories;
using RoomBooking.Services.Time;

namespace RoomBooking.Services.Services;

public sealed class RoomBookingService : IRoomBookingService
{
    private readonly IRoomRepository roomRepository;
    private readonly IReservationRepository reservationRepository;
    private readonly IReservationVersionRepository reservationVersionRepository;
    private readonly IBookingUnitOfWork bookingUnitOfWork;
    private readonly IClock clock;

    public RoomBookingService(
        IRoomRepository roomRepository,
        IReservationRepository reservationRepository,
        IReservationVersionRepository reservationVersionRepository,
        IBookingUnitOfWork bookingUnitOfWork,
        IClock clock)
    {
        this.roomRepository = roomRepository;
        this.reservationRepository = reservationRepository;
        this.reservationVersionRepository = reservationVersionRepository;
        this.bookingUnitOfWork = bookingUnitOfWork;
        this.clock = clock;
    }

    public async Task<GetRoomsResult> GetRoomsAsync(
        DateTimeOffset? start,
        DateTimeOffset? end,
        CancellationToken cancellationToken = default)
    {
        if (!ReservationRequestRules.TryNormalizeRoomsRange(start, end, out var startUtc, out var endUtc, out var failure))
        {
            return new GetRoomsResult.ValidationFailed(failure!);
        }

        if (!startUtc.HasValue)
        {
            var allRooms = await roomRepository.GetAllAsync(cancellationToken);
            return new GetRoomsResult.Success(allRooms);
        }

        var availableRooms = await roomRepository.GetAvailableAsync(startUtc.Value, endUtc!.Value, cancellationToken);
        return new GetRoomsResult.Success(availableRooms);
    }

    public async Task<GetReservationsResult> GetReservationsAsync(
        Guid roomId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        if (!ReservationRequestRules.TryNormalizeReservationQuery(roomId, start, end, limit, offset, out var query, out var failure))
        {
            return new GetReservationsResult.ValidationFailed(failure!);
        }

        var roomExists = await roomRepository.ExistsAsync(roomId, cancellationToken);
        if (!roomExists)
        {
            return new GetReservationsResult.NotFound(new NotFoundFailure("room_not_found", "Room was not found."));
        }

        var reservations = await reservationRepository.GetForRoomAsync(query, cancellationToken);
        return new GetReservationsResult.Success(reservations);
    }

    public async Task<CreateReservationResult> CreateReservationAsync(
        Guid roomId,
        DateTimeOffset start,
        DateTimeOffset end,
        string title,
        CancellationToken cancellationToken = default)
    {
        if (!ReservationRequestRules.TryNormalizeCreateReservation(
                roomId,
                start,
                end,
                title,
                clock.UtcNow,
                out var command,
                out var failure))
        {
            return new CreateReservationResult.ValidationFailed(failure!);
        }

        return await bookingUnitOfWork.ExecuteInImmediateTransactionAsync<CreateReservationResult>(async ct =>
        {
            var roomExists = await roomRepository.ExistsAsync(roomId, ct);
            if (!roomExists)
            {
                return new CreateReservationResult.NotFound(new NotFoundFailure("room_not_found", "Room was not found."));
            }

            var hasOverlap = await reservationRepository.HasOverlapAsync(roomId, command.StartUtc, command.EndUtc, ct);
            if (hasOverlap)
            {
                return new CreateReservationResult.Conflict(new ConflictFailure("reservation_conflict", "Room is not available in the requested time range."));
            }

            var reservation = await reservationRepository.AddAsync(command, ct);
            await reservationVersionRepository.IncrementGlobalAndRoomVersionAsync(roomId, ct);
            return new CreateReservationResult.Success(reservation);
        }, cancellationToken);
    }
}