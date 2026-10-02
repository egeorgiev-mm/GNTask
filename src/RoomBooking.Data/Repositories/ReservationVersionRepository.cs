using Microsoft.EntityFrameworkCore;
using RoomBooking.Data.Entities;
using RoomBooking.Services.Repositories;

namespace RoomBooking.Data.Repositories;

public sealed class ReservationVersionRepository : IReservationVersionRepository
{
    private readonly RoomBookingDbContext _db;

    public ReservationVersionRepository(RoomBookingDbContext db) => _db = db;

    public async Task<long> GetGlobalVersionAsync(CancellationToken cancellationToken = default)
    {
        var v = await _db.ReservationVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoomId == ReservationVersion.GlobalScope, cancellationToken);
        return v?.Counter ?? 0L;
    }

    public async Task<long?> GetRoomVersionAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var v = await _db.ReservationVersions.AsNoTracking().FirstOrDefaultAsync(x => x.RoomId == roomId, cancellationToken);
        return v?.Counter;
    }

    public async Task IncrementGlobalAndRoomVersionAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ReservationVersions (Id, RoomId, Counter)
            VALUES ({Guid.NewGuid()}, {ReservationVersion.GlobalScope}, 1)
            ON CONFLICT(RoomId) DO UPDATE SET Counter = Counter + 1;
            """, cancellationToken);

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ReservationVersions (Id, RoomId, Counter)
            VALUES ({Guid.NewGuid()}, {roomId}, 1)
            ON CONFLICT(RoomId) DO UPDATE SET Counter = Counter + 1;
            """, cancellationToken);
    }
}
