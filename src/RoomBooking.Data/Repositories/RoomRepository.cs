using Microsoft.EntityFrameworkCore;
using RoomBooking.Data.Entities;
using RoomBooking.Services.Models;
using RoomBooking.Services.Repositories;

namespace RoomBooking.Data.Repositories;

public sealed class RoomRepository : IRoomRepository
{
    private readonly RoomBookingDbContext _db;

    public RoomRepository(RoomBookingDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoomModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Rooms.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoomModel(r.Id, r.Name, r.Capacity))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoomModel>> GetAvailableAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken = default)
    {
        // rooms without any overlapping reservation
        var q = from room in _db.Rooms.AsNoTracking()
                where !_db.Reservations.Any(existing => existing.RoomId == room.Id && existing.StartUtc < endUtc && existing.EndUtc > startUtc)
            orderby room.Name
            select new RoomModel(room.Id, room.Name, room.Capacity);

        return await q.ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return await _db.Rooms.AsNoTracking().AnyAsync(r => r.Id == roomId, cancellationToken);
    }
}
