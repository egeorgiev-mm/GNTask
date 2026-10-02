using NSubstitute;
using RoomBooking.Services.Contracts;
using RoomBooking.Services.Repositories;
using RoomBooking.Services.Services;
using Shouldly;

namespace RoomBooking.Tests.Services;

public sealed class ReservationVersionTokenServiceTests
{
    private readonly IReservationVersionRepository reservationVersionRepository = Substitute.For<IReservationVersionRepository>();
    private readonly IRoomRepository roomRepository = Substitute.For<IRoomRepository>();

    private readonly ReservationVersionTokenService service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ReservationVersionTokenServiceTests()
    {
        service = new ReservationVersionTokenService(reservationVersionRepository, roomRepository);
    }

    [Fact]
    public async Task GetRoomsToken_NoRange_ReturnsStaticToken()
    {
        var result = await service.GetRoomsTokenAsync(null, null, Ct);

        var success = result.ShouldBeOfType<GetRoomsVersionTokenResult.Success>();
        success.Token.ShouldBe("rooms-v1-static");
        await reservationVersionRepository.DidNotReceiveWithAnyArgs().GetGlobalVersionAsync(Ct);
    }

    [Fact]
    public async Task GetRoomsToken_SameInput_ReturnsStableToken()
    {
        reservationVersionRepository.GetGlobalVersionAsync(Arg.Any<CancellationToken>()).Returns(5L);

        var start = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(2));
        var end = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(2));

        var first = await service.GetRoomsTokenAsync(start, end, Ct);
        var second = await service.GetRoomsTokenAsync(start, end, Ct);

        var firstToken = first.ShouldBeOfType<GetRoomsVersionTokenResult.Success>().Token;
        var secondToken = second.ShouldBeOfType<GetRoomsVersionTokenResult.Success>().Token;
        firstToken.ShouldBe(secondToken);
    }

    [Fact]
    public async Task GetRoomsToken_VersionChanges_ChangesToken()
    {
        reservationVersionRepository.GetGlobalVersionAsync(Arg.Any<CancellationToken>()).Returns(5L, 6L);
        var start = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero);

        var first = await service.GetRoomsTokenAsync(start, end, Ct);
        var second = await service.GetRoomsTokenAsync(start, end, Ct);

        first.ShouldBeOfType<GetRoomsVersionTokenResult.Success>().Token
            .ShouldNotBe(second.ShouldBeOfType<GetRoomsVersionTokenResult.Success>().Token);
    }

    [Fact]
    public async Task GetRoomsToken_OneBoundaryOnly_ReturnsValidationFailed()
    {
        var result = await service.GetRoomsTokenAsync(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero), null, Ct);

        result.ShouldBeOfType<GetRoomsVersionTokenResult.ValidationFailed>();
        await reservationVersionRepository.DidNotReceiveWithAnyArgs().GetGlobalVersionAsync(Ct);
    }

    [Fact]
    public async Task GetRoomReservationsToken_UnknownRoom_ReturnsNotFound()
    {
        var roomId = Guid.NewGuid();
        roomRepository.ExistsAsync(roomId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await service.GetRoomReservationsTokenAsync(roomId, null, null, 10, 0, Ct);

        result.ShouldBeOfType<GetRoomReservationsVersionTokenResult.NotFound>();
        await reservationVersionRepository.DidNotReceiveWithAnyArgs().GetRoomVersionAsync(default, Ct);
    }

    [Fact]
    public async Task GetRoomReservationsToken_InvalidPaging_ReturnsValidationFailed()
    {
        var roomId = Guid.NewGuid();

        var invalidOffset = await service.GetRoomReservationsTokenAsync(roomId, null, null, 10, -1, Ct);
        var invalidLimit = await service.GetRoomReservationsTokenAsync(roomId, null, null, 0, 0, Ct);

        invalidOffset.ShouldBeOfType<GetRoomReservationsVersionTokenResult.ValidationFailed>();
        invalidLimit.ShouldBeOfType<GetRoomReservationsVersionTokenResult.ValidationFailed>();
        await roomRepository.DidNotReceiveWithAnyArgs().ExistsAsync(default, Ct);
    }

    [Fact]
    public async Task GetRoomReservationsToken_SameInput_ReturnsStableToken()
    {
        var roomId = Guid.NewGuid();
        roomRepository.ExistsAsync(roomId, Arg.Any<CancellationToken>()).Returns(true);
        reservationVersionRepository.GetRoomVersionAsync(roomId, Arg.Any<CancellationToken>()).Returns(9L);

        var first = await service.GetRoomReservationsTokenAsync(
            roomId,
            new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(2)),
            20,
            3,
            Ct);

        var second = await service.GetRoomReservationsTokenAsync(
            roomId,
            new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(2)),
            20,
            3,
            Ct);

        first.ShouldBeOfType<GetRoomReservationsVersionTokenResult.Success>().Token
            .ShouldBe(second.ShouldBeOfType<GetRoomReservationsVersionTokenResult.Success>().Token);
    }

    [Fact]
    public async Task GetRoomReservationsToken_DifferentInputs_ProduceDifferentTokens()
    {
        var roomA = Guid.NewGuid();
        var roomB = Guid.NewGuid();

        roomRepository.ExistsAsync(roomA, Arg.Any<CancellationToken>()).Returns(true);
        roomRepository.ExistsAsync(roomB, Arg.Any<CancellationToken>()).Returns(true);
        reservationVersionRepository.GetRoomVersionAsync(roomA, Arg.Any<CancellationToken>()).Returns(9L);
        reservationVersionRepository.GetRoomVersionAsync(roomB, Arg.Any<CancellationToken>()).Returns(9L);

        var tokenRoomA = await service.GetRoomReservationsTokenAsync(roomA, null, null, 20, 0, Ct);
        var tokenRoomB = await service.GetRoomReservationsTokenAsync(roomB, null, null, 20, 0, Ct);
        var tokenDifferentRange = await service.GetRoomReservationsTokenAsync(
            roomA,
            new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero),
            20,
            0,
            Ct);
        var tokenDifferentPaging = await service.GetRoomReservationsTokenAsync(roomA, null, null, 21, 1, Ct);

        tokenRoomA.ShouldBeOfType<GetRoomReservationsVersionTokenResult.Success>().Token
            .ShouldNotBe(tokenRoomB.ShouldBeOfType<GetRoomReservationsVersionTokenResult.Success>().Token);
        tokenRoomA.ShouldBeOfType<GetRoomReservationsVersionTokenResult.Success>().Token
            .ShouldNotBe(tokenDifferentRange.ShouldBeOfType<GetRoomReservationsVersionTokenResult.Success>().Token);
        tokenRoomA.ShouldBeOfType<GetRoomReservationsVersionTokenResult.Success>().Token
            .ShouldNotBe(tokenDifferentPaging.ShouldBeOfType<GetRoomReservationsVersionTokenResult.Success>().Token);
    }

    [Fact]
    public async Task GetRoomReservationsToken_RoomVersionChanges_ChangesToken()
    {
        var roomId = Guid.NewGuid();
        roomRepository.ExistsAsync(roomId, Arg.Any<CancellationToken>()).Returns(true);
        reservationVersionRepository.GetRoomVersionAsync(roomId, Arg.Any<CancellationToken>()).Returns(4L, 5L);

        var first = await service.GetRoomReservationsTokenAsync(roomId, null, null, 20, 0, Ct);
        var second = await service.GetRoomReservationsTokenAsync(roomId, null, null, 20, 0, Ct);

        first.ShouldBeOfType<GetRoomReservationsVersionTokenResult.Success>().Token
            .ShouldNotBe(second.ShouldBeOfType<GetRoomReservationsVersionTokenResult.Success>().Token);
    }
}