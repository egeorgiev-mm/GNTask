using Microsoft.EntityFrameworkCore;
using RoomBooking.Data.Entities;

namespace RoomBooking.Data;

public sealed class RoomBookingDbInitializer
{
    public static readonly Guid SmallRoomId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ConferenceRoomId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid AuditoriumId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly RoomBookingDbContext _db;

    public RoomBookingDbInitializer(RoomBookingDbContext db) => _db = db;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Run migrations only; do not call EnsureCreated here (EnsureCreated bypasses migrations)
        await _db.Database.MigrateAsync(cancellationToken);

        // Ensure WAL mode for SQLite databases.
        var conn = _db.Database.GetDbConnection();
        await _db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode = WAL;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }

        // Seed data idempotently (kept in a separable method so tests can create schema via EnsureCreated first)
        await SeedAsync(cancellationToken);
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Idempotent seed: insert rooms if they do not exist
        var rooms = new[]
        {
            new Room { Id = SmallRoomId, Name = "Small Room", Capacity = 4 },
            new Room { Id = ConferenceRoomId, Name = "Conference Room", Capacity = 10 },
            new Room { Id = AuditoriumId, Name = "Auditorium", Capacity = 50 }
        };

        foreach (var r in rooms)
        {
            if (!await _db.Rooms.AnyAsync(x => x.Name == r.Name, cancellationToken))
            {
                _db.Rooms.Add(r);
            }
        }

        if (!await _db.ReservationVersions.AnyAsync(x => x.RoomId == ReservationVersion.GlobalScope, cancellationToken))
        {
            _db.ReservationVersions.Add(new ReservationVersion
            {
                Id = Guid.NewGuid(),
                RoomId = ReservationVersion.GlobalScope,
                Counter = 0
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
