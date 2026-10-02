using NSubstitute;
using RoomBooking.Services.Contracts;
using RoomBooking.Services.Models;
using RoomBooking.Services.Repositories;
using RoomBooking.Services.Services;
using RoomBooking.Services.Time;
using Shouldly;

namespace RoomBooking.Tests.Services;

public sealed class RoomBookingServiceTests
{
    private readonly IRoomRepository roomRepository = Substitute.For<IRoomRepository>();
    private readonly IReservationRepository reservationRepository = Substitute.For<IReservationRepository>();
    private readonly IReservationVersionRepository reservationVersionRepository = Substitute.For<IReservationVersionRepository>();
    private readonly IBookingUnitOfWork bookingUnitOfWork = Substitute.For<IBookingUnitOfWork>();
    private readonly IClock clock = Substitute.For<IClock>();

    private readonly RoomBookingService service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomBookingServiceTests()
    {
        service = new RoomBookingService(
            roomRepository,
            reservationRepository,
            reservationVersionRepository,
            bookingUnitOfWork,
            clock);
    }

    [Fact]
    public async Task CreateReservation_ValidationInvalidRange_ReturnsValidationFailedWithoutRepositoryCalls()
    {
        var result = await service.CreateReservationAsync(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero),
            "Team sync",
            Ct);

        result.ShouldBeOfType<CreateReservationResult.ValidationFailed>();
        await bookingUnitOfWork.DidNotReceiveWithAnyArgs().ExecuteInImmediateTransactionAsync(Arg.Any<Func<CancellationToken, Task<CreateReservationResult>>>(), Ct);
        await roomRepository.DidNotReceiveWithAnyArgs().ExistsAsync(default, Ct);
        await reservationRepository.DidNotReceiveWithAnyArgs().HasOverlapAsync(default, default, default, Ct);
    }

    [Fact]
    public async Task CreateReservation_ValidationTitleMissing_ReturnsValidationFailedWithoutRepositoryCalls()
    {
        var result = await service.CreateReservationAsync(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero),
            "  ",
            Ct);

        var failure = result.ShouldBeOfType<CreateReservationResult.ValidationFailed>();
        failure.Failure.Code.ShouldBe("title_required");
        await bookingUnitOfWork.DidNotReceiveWithAnyArgs().ExecuteInImmediateTransactionAsync(Arg.Any<Func<CancellationToken, Task<CreateReservationResult>>>(), Ct);
    }

    [Fact]
    public async Task CreateReservation_ValidationTitleTooLong_ReturnsValidationFailedWithoutRepositoryCalls()
    {
        var tooLongTitle = new string('x', 201);

        var result = await service.CreateReservationAsync(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero),
            tooLongTitle,
            Ct);

        var failure = result.ShouldBeOfType<CreateReservationResult.ValidationFailed>();
        failure.Failure.Code.ShouldBe("title_too_long");
        await bookingUnitOfWork.DidNotReceiveWithAnyArgs().ExecuteInImmediateTransactionAsync(Arg.Any<Func<CancellationToken, Task<CreateReservationResult>>>(), Ct);
    }

    [Theory]
    [InlineData(10, 30, 11, 30, true)]
    [InlineData(9, 30, 10, 30, true)]
    [InlineData(9, 0, 12, 0, true)]
    [InlineData(10, 15, 10, 45, true)]
    [InlineData(11, 0, 12, 0, false)]
    [InlineData(9, 0, 10, 0, false)]
    public async Task CreateReservation_OverlapOutcomes_MatchesHalfOpenBehavior(
        int requestStartHour,
        int requestStartMinute,
        int requestEndHour,
        int requestEndMinute,
        bool expectedConflict)
    {
        var roomId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(createdAt);
        roomRepository.ExistsAsync(roomId, Arg.Any<CancellationToken>()).Returns(true);

        bookingUnitOfWork.ExecuteInImmediateTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<CreateReservationResult>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<CreateReservationResult>>>()(call.Arg<CancellationToken>()));

        var existingStart = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        var existingEnd = new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero);

        reservationRepository.HasOverlapAsync(roomId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var requestStart = call.ArgAt<DateTimeOffset>(1);
                var requestEnd = call.ArgAt<DateTimeOffset>(2);
                return existingStart < requestEnd && existingEnd > requestStart;
            });

        reservationRepository.AddAsync(Arg.Any<CreateReservationCommand>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var command = call.Arg<CreateReservationCommand>();
                return new ReservationModel(Guid.NewGuid(), command.RoomId, command.StartUtc, command.EndUtc, command.Title, command.CreatedAtUtc);
            });

        var result = await service.CreateReservationAsync(
            roomId,
            new DateTimeOffset(2026, 10, 2, requestStartHour, requestStartMinute, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 2, requestEndHour, requestEndMinute, 0, TimeSpan.Zero),
            "Team sync",
            Ct);

        if (expectedConflict)
        {
            result.ShouldBeOfType<CreateReservationResult.Conflict>();
            await reservationVersionRepository.DidNotReceiveWithAnyArgs().IncrementGlobalAndRoomVersionAsync(default, Ct);
            await reservationRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
        }
        else
        {
            result.ShouldBeOfType<CreateReservationResult.Success>();
            await reservationVersionRepository.Received(1).IncrementGlobalAndRoomVersionAsync(roomId, Ct);
        }
    }

    [Fact]
    public async Task CreateReservation_RoomMissing_ReturnsNotFound()
    {
        var roomId = Guid.NewGuid();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));

        bookingUnitOfWork.ExecuteInImmediateTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<CreateReservationResult>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<CreateReservationResult>>>()(call.Arg<CancellationToken>()));

        roomRepository.ExistsAsync(roomId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await service.CreateReservationAsync(
            roomId,
            new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero),
            "Team sync",
            Ct);

        result.ShouldBeOfType<CreateReservationResult.NotFound>();
        await reservationRepository.DidNotReceiveWithAnyArgs().HasOverlapAsync(default, default, default, Ct);
    }

    [Fact]
    public async Task CreateReservation_OffsetInput_StoresUtcAndTrimmedTitleAndClockCreatedAt()
    {
        var roomId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(createdAt);

        bookingUnitOfWork.ExecuteInImmediateTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<CreateReservationResult>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<CreateReservationResult>>>()(call.Arg<CancellationToken>()));

        roomRepository.ExistsAsync(roomId, Arg.Any<CancellationToken>()).Returns(true);
        reservationRepository.HasOverlapAsync(roomId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        CreateReservationCommand? capturedCommand = null;
        reservationRepository.AddAsync(Arg.Any<CreateReservationCommand>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedCommand = call.Arg<CreateReservationCommand>();
                return new ReservationModel(Guid.NewGuid(), roomId, capturedCommand.StartUtc, capturedCommand.EndUtc, capturedCommand.Title, capturedCommand.CreatedAtUtc);
            });

        var result = await service.CreateReservationAsync(
            roomId,
            new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.FromHours(2)),
            "  Trim me  ",
            Ct);

        result.ShouldBeOfType<CreateReservationResult.Success>();
        capturedCommand.ShouldNotBeNull();
        capturedCommand.StartUtc.Offset.ShouldBe(TimeSpan.Zero);
        capturedCommand.StartUtc.ShouldBe(new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero));
        capturedCommand.EndUtc.ShouldBe(new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero));
        capturedCommand.Title.ShouldBe("Trim me");
        capturedCommand.CreatedAtUtc.ShouldBe(createdAt);
        await reservationVersionRepository.Received(1).IncrementGlobalAndRoomVersionAsync(roomId, Ct);
    }

    [Fact]
    public async Task GetRooms_NoRange_ReturnsAllRooms()
    {
        var rooms = new List<RoomModel> { new(Guid.NewGuid(), "Blue", 8) };
        roomRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(rooms);

        var result = await service.GetRoomsAsync(null, null, Ct);

        var success = result.ShouldBeOfType<GetRoomsResult.Success>();
        success.Rooms.ShouldBe(rooms);
        await roomRepository.Received(1).GetAllAsync(Ct);
        await roomRepository.DidNotReceiveWithAnyArgs().GetAvailableAsync(default, default, Ct);
    }

    [Fact]
    public async Task GetRooms_WithRange_ReturnsAvailableRoomsUsingUtcRange()
    {
        var available = new List<RoomModel> { new(Guid.NewGuid(), "Green", 10) };
        roomRepository.GetAvailableAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(available);

        var result = await service.GetRoomsAsync(
            new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.FromHours(2)),
            Ct);

        var success = result.ShouldBeOfType<GetRoomsResult.Success>();
        success.Rooms.ShouldBe(available);
        await roomRepository.Received(1).GetAvailableAsync(
            new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero),
            Ct);
    }

    [Fact]
    public async Task GetRooms_OnlyOneRangeBoundary_ReturnsValidationFailed()
    {
        var result = await service.GetRoomsAsync(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero), null, Ct);

        result.ShouldBeOfType<GetRoomsResult.ValidationFailed>();
        await roomRepository.DidNotReceiveWithAnyArgs().GetAllAsync(Ct);
        await roomRepository.DidNotReceiveWithAnyArgs().GetAvailableAsync(default, default, Ct);
    }

    [Fact]
    public async Task GetReservations_RoomMissing_ReturnsNotFound()
    {
        var roomId = Guid.NewGuid();
        roomRepository.ExistsAsync(roomId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await service.GetReservationsAsync(roomId, null, null, 25, 0, Ct);

        result.ShouldBeOfType<GetReservationsResult.NotFound>();
        await reservationRepository.DidNotReceiveWithAnyArgs().GetForRoomAsync(default!, Ct);
    }

    [Fact]
    public async Task GetReservations_InvalidOffsetOrLimit_ReturnsValidationFailed()
    {
        var roomId = Guid.NewGuid();

        var invalidOffset = await service.GetReservationsAsync(roomId, null, null, 10, -1, Ct);
        var invalidLimit = await service.GetReservationsAsync(roomId, null, null, 0, 0, Ct);

        invalidOffset.ShouldBeOfType<GetReservationsResult.ValidationFailed>();
        invalidLimit.ShouldBeOfType<GetReservationsResult.ValidationFailed>();
        await roomRepository.DidNotReceiveWithAnyArgs().ExistsAsync(default, Ct);
    }

    [Fact]
    public async Task GetReservations_LimitAboveMax_ClampsToMax100()
    {
        var roomId = Guid.NewGuid();
        roomRepository.ExistsAsync(roomId, Arg.Any<CancellationToken>()).Returns(true);

        ReservationQuery? capturedQuery = null;
        var paged = new PagedResult<ReservationModel>(Array.Empty<ReservationModel>(), 100, 7, 0);

        reservationRepository.GetForRoomAsync(Arg.Any<ReservationQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedQuery = call.Arg<ReservationQuery>();
                return paged;
            });

        var result = await service.GetReservationsAsync(
            roomId,
            new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(2)),
            999,
            7,
            Ct);

        result.ShouldBeOfType<GetReservationsResult.Success>();
        capturedQuery.ShouldNotBeNull();
        capturedQuery.Limit.ShouldBe(100);
        capturedQuery.Offset.ShouldBe(7);
        capturedQuery.StartUtc.ShouldBe(new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero));
        capturedQuery.EndUtc.ShouldBe(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero));
    }
}