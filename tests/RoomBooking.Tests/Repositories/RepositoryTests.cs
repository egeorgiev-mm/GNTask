using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoomBooking.Data;
using RoomBooking.Data.Entities;
using RoomBooking.Data.Repositories;
using RoomBooking.Services.Contracts;
using Shouldly;

namespace RoomBooking.Tests.Repositories;

public sealed class RepositoryTests : IDisposable
{
    private readonly string _dbFile;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RepositoryTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"roombooking_{Guid.NewGuid():N}.db");
    }

    private RoomBookingDbContext CreateContext(int defaultTimeoutSeconds = 30)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = _dbFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = defaultTimeoutSeconds
        }.ToString();

        var options = new DbContextOptionsBuilder<RoomBookingDbContext>()
            .UseSqlite(cs)
            .Options;

        return new RoomBookingDbContext(options);
    }

    [Fact]
    public async Task OverlapChecks_WorkAsHalfOpenIntervals()
    {
        // Arrange
        await using var db = CreateContext();
        var init = new RoomBookingDbInitializer(db);
        await init.InitializeAsync(Ct);

        var room = new Room { Id = Guid.NewGuid(), Name = "R1", Capacity = 2 };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(Ct);

        var repo = new ReservationRepository(db);

        db.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            StartUtc = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero),
            Title = "e"
        });
        await db.SaveChangesAsync(Ct);

        // Act
        var overlapInside = await repo.HasOverlapAsync(room.Id, new DateTimeOffset(2026, 1, 1, 10, 30, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 1, 11, 30, 0, TimeSpan.Zero), Ct);
        var overlapLeftEdge = await repo.HasOverlapAsync(room.Id, new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 1, 10, 30, 0, TimeSpan.Zero), Ct);
        var noOverlapAtEnd = await repo.HasOverlapAsync(room.Id, new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), Ct);
        var noOverlapAtStart = await repo.HasOverlapAsync(room.Id, new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), Ct);

        // Assert
        overlapInside.ShouldBeTrue();
        overlapLeftEdge.ShouldBeTrue();
        noOverlapAtEnd.ShouldBeFalse();
        noOverlapAtStart.ShouldBeFalse();
    }

    [Fact]
    public async Task GetForRoom_UsesOverlapWindowSemantics_ForOptionalBounds()
    {
        // Arrange
        await using var db = CreateContext();
        var init = new RoomBookingDbInitializer(db);
        await init.InitializeAsync(Ct);

        var room = new Room { Id = Guid.NewGuid(), Name = "Filter", Capacity = 2 };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(Ct);

        var first = new Reservation
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            StartUtc = new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 1, 1, 10, 30, 0, TimeSpan.Zero),
            Title = "overlaps"
        };

        var second = new Reservation
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            StartUtc = new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            Title = "outside"
        };

        db.Reservations.AddRange(first, second);
        await db.SaveChangesAsync(Ct);

        var repo = new ReservationRepository(db);

        // Act
        var window = await repo.GetForRoomAsync(
            new ReservationQuery(
                room.Id,
                new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero),
                50,
                0),
            Ct);

        // Assert
        window.Items.Select(x => x.Id).ShouldBe([first.Id]);

        // Act
        var onlyStart = await repo.GetForRoomAsync(
            new ReservationQuery(
                room.Id,
                new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
                null,
                50,
                0),
            Ct);

        // Assert
        onlyStart.Items.Select(x => x.Id).ShouldBe([first.Id, second.Id]);

        // Act
        var onlyEnd = await repo.GetForRoomAsync(
            new ReservationQuery(
                room.Id,
                null,
                new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero),
                50,
                0),
            Ct);

        // Assert
        onlyEnd.Items.Select(x => x.Id).ShouldBe([first.Id]);
    }

    [Fact]
    public async Task Paging_OrdersByStartThenId()
    {
        // Arrange
        await using var db = CreateContext();
        var init = new RoomBookingDbInitializer(db);
        await init.InitializeAsync(Ct);

        var room = new Room { Id = Guid.NewGuid(), Name = "Paging", Capacity = 2 };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(Ct);

        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var id3 = Guid.Parse("00000000-0000-0000-0000-000000000003");

        db.Reservations.AddRange(
            new Reservation
            {
                Id = id2,
                RoomId = room.Id,
                StartUtc = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
                EndUtc = new DateTimeOffset(2026, 1, 1, 10, 30, 0, TimeSpan.Zero),
                Title = "b"
            },
            new Reservation
            {
                Id = id1,
                RoomId = room.Id,
                StartUtc = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
                EndUtc = new DateTimeOffset(2026, 1, 1, 10, 45, 0, TimeSpan.Zero),
                Title = "a"
            },
            new Reservation
            {
                Id = id3,
                RoomId = room.Id,
                StartUtc = new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero),
                EndUtc = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
                Title = "c"
            });

        await db.SaveChangesAsync(Ct);
        var repo = new ReservationRepository(db);

        // Act
        var page = await repo.GetForRoomAsync(new ReservationQuery(room.Id, null, null, 10, 0), Ct);

        // Assert
        page.Items.Select(x => x.Id).ShouldBe([id1, id2, id3]);
    }

    [Fact]
    public async Task Version_Increments_Persist()
    {
        // Arrange
        await using var db = CreateContext();
        var init = new RoomBookingDbInitializer(db);
        await init.InitializeAsync(Ct);

        var room = new Room { Id = Guid.NewGuid(), Name = "V", Capacity = 2 };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(Ct);

        var versionRepo = new ReservationVersionRepository(db);

        // Act
        await versionRepo.IncrementGlobalAndRoomVersionAsync(room.Id, Ct);

        // Assert
        (await versionRepo.GetGlobalVersionAsync(Ct)).ShouldBe(1L);
        (await versionRepo.GetRoomVersionAsync(room.Id, Ct)).ShouldBe(1L);

        // Act
        await versionRepo.IncrementGlobalAndRoomVersionAsync(room.Id, Ct);

        // Assert
        (await versionRepo.GetGlobalVersionAsync(Ct)).ShouldBe(2L);
        (await versionRepo.GetRoomVersionAsync(room.Id, Ct)).ShouldBe(2L);
    }

    [Fact]
    public async Task DateTimeOffset_Ordering_IsUtcCorrect()
    {
        // Arrange
        await using var db = CreateContext();
        var init = new RoomBookingDbInitializer(db);
        await init.InitializeAsync(Ct);

        var room = new Room { Id = Guid.NewGuid(), Name = "Tz", Capacity = 2 };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(Ct);

        var utcEarlierLocallyLater = new Reservation
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            StartUtc = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(5)),
            EndUtc = new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.FromHours(5)),
            Title = "offset+5"
        };

        var utcLaterLocallyEarlier = new Reservation
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            StartUtc = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero),
            Title = "utc"
        };

        db.Reservations.AddRange(utcLaterLocallyEarlier, utcEarlierLocallyLater);
        await db.SaveChangesAsync(Ct);
        var repo = new ReservationRepository(db);

        // Act
        var page = await repo.GetForRoomAsync(new ReservationQuery(room.Id, null, null, 10, 0), Ct);

        // Assert
        page.Items.Select(x => x.Id).ShouldBe([utcEarlierLocallyLater.Id, utcLaterLocallyEarlier.Id]);
    }

    [Fact]
    public async Task Concurrency_DoubleBooking_AllowsOnlyOne()
    {
        // Arrange
        await using (var setup = CreateContext())
        {
            var init = new RoomBookingDbInitializer(setup);
            await init.InitializeAsync(Ct);
            setup.Rooms.Add(new Room { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Name = "C", Capacity = 2 });
            await setup.SaveChangesAsync(Ct);
        }

        var roomId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var start = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(1);

        async Task<bool> TryCreateAsync(string title)
        {
            await using var db = CreateContext();
            var uow = new BookingUnitOfWork(db);
            return await uow.ExecuteInImmediateTransactionAsync(async ct =>
            {
                var repo = new ReservationRepository(db);
                if (await repo.HasOverlapAsync(roomId, start, end, ct))
                {
                    return false;
                }

                await repo.AddAsync(new CreateReservationCommand(roomId, start, end, title, DateTimeOffset.UtcNow), ct);
                return true;
            }, Ct);
        }

        // Act
        var results = await Task.WhenAll(TryCreateAsync("t1"), TryCreateAsync("t2"));

        // Assert
        results.Count(x => x).ShouldBe(1);
        results.Count(x => !x).ShouldBe(1);

        await using var verify = CreateContext();
        (await verify.Reservations.CountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task BookingUnitOfWork_SecondWriterBusyThenSucceedsAfterCommit()
    {
        // Arrange
        await using (var setup = CreateContext())
        {
            var init = new RoomBookingDbInitializer(setup);
            await init.InitializeAsync(Ct);
        }

        await using var db1 = CreateContext(defaultTimeoutSeconds: 30);
        await using var db2FastTimeout = CreateContext(defaultTimeoutSeconds: 1);
        await using var db2SlowTimeout = CreateContext(defaultTimeoutSeconds: 30);

        var uow1 = new BookingUnitOfWork(db1);
        var uow2FastTimeout = new BookingUnitOfWork(db2FastTimeout);
        var uow2SlowTimeout = new BookingUnitOfWork(db2SlowTimeout);

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstWriter = Task.Run(async () =>
        {
            await uow1.ExecuteInImmediateTransactionAsync(async ct =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
                return 0;
            }, Ct);
        }, Ct);

        await entered.Task.WaitAsync(Ct);

        // Act
        var busy = await Should.ThrowAsync<SqliteException>(async () =>
            await uow2FastTimeout.ExecuteInImmediateTransactionAsync(async _ =>
            {
                await Task.CompletedTask;
                return 1;
            }, Ct));

        // Assert
        (busy.SqliteErrorCode == 5 || busy.SqliteErrorCode == 6).ShouldBeTrue();

        // Act
        release.TrySetResult();
        await firstWriter.WaitAsync(Ct);

        var retryResult = await uow2SlowTimeout.ExecuteInImmediateTransactionAsync(async _ =>
        {
            await Task.CompletedTask;
            return 42;
        }, Ct);

        // Assert
        retryResult.ShouldBe(42);
    }

    [Fact]
    public async Task Add_Persists_SuppliedCreatedAtUtc()
    {
        // Arrange
        await using var db = CreateContext();
        var init = new RoomBookingDbInitializer(db);
        await init.InitializeAsync(Ct);

        var room = new Room { Id = Guid.NewGuid(), Name = "CA", Capacity = 2 };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(Ct);

        var repo = new ReservationRepository(db);
        var supplied = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

        // Act
        await repo.AddAsync(new CreateReservationCommand(room.Id, supplied, supplied.AddHours(1), "created-at-test", supplied), Ct);

        // Assert
        var stored = await db.Reservations
            .AsNoTracking()
            .Where(r => r.RoomId == room.Id)
            .Select(r => r.CreatedAtUtc)
            .FirstAsync(Ct);

        stored.ShouldBe(supplied);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        TryDelete(_dbFile);
        TryDelete(_dbFile + "-wal");
        TryDelete(_dbFile + "-shm");
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}