using Microsoft.EntityFrameworkCore;
using RoomBooking.Data.Entities;
using RoomBooking.Services.Contracts;
using RoomBooking.Services.Models;
using RoomBooking.Services.Repositories;

namespace RoomBooking.Data.Repositories;

public sealed class ReservationRepository : IReservationRepository
{
    private readonly RoomBookingDbContext _db;

    public ReservationRepository(RoomBookingDbContext db) => _db = db;

    public async Task<PagedResult<ReservationModel>> GetForRoomAsync(ReservationQuery query, CancellationToken cancellationToken = default)
    {
        var baseQ = _db.Reservations.AsNoTracking().Where(r => r.RoomId == query.RoomId);

        if (query.StartUtc.HasValue)
            baseQ = baseQ.Where(r => r.EndUtc > query.StartUtc.Value);

        if (query.EndUtc.HasValue)
            baseQ = baseQ.Where(r => r.StartUtc < query.EndUtc.Value);

        var total = await baseQ.CountAsync(cancellationToken);

        var items = await baseQ.OrderBy(r => r.StartUtc).ThenBy(r => r.Id)
            .Skip(query.Offset).Take(query.Limit)
            .Select(r => new ReservationModel(r.Id, r.RoomId, r.StartUtc, r.EndUtc, r.Title, r.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<ReservationModel>(items, query.Limit, query.Offset, total);
    }

    public async Task<bool> HasOverlapAsync(Guid roomId, DateTimeOffset requestedStartUtc, DateTimeOffset requestedEndUtc, CancellationToken cancellationToken = default)
    {
        return await _db.Reservations.AsNoTracking()
            .AnyAsync(r => r.RoomId == roomId && r.StartUtc < requestedEndUtc && r.EndUtc > requestedStartUtc, cancellationToken);
    }

    public async Task<ReservationModel> AddAsync(CreateReservationCommand command, CancellationToken cancellationToken = default)
    {
        var entity = new Reservation
        {
            Id = Guid.NewGuid(),
            RoomId = command.RoomId,
            StartUtc = command.StartUtc,
            EndUtc = command.EndUtc,
            Title = command.Title,
            CreatedAtUtc = command.CreatedAtUtc
        };

        _db.Reservations.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return new ReservationModel(entity.Id, entity.RoomId, entity.StartUtc, entity.EndUtc, entity.Title, entity.CreatedAtUtc);
    }
}
