using System.Security.Cryptography;
using System.Text;
using RoomBooking.Services.Contracts;
using RoomBooking.Services.Repositories;

namespace RoomBooking.Services.Services;

public sealed class ReservationVersionTokenService : IReservationVersionTokenService
{
    private const string ContractVersion = "v1";
    private const string RoomsNoRangeToken = "rooms-v1-static";

    private readonly IReservationVersionRepository reservationVersionRepository;
    private readonly IRoomRepository roomRepository;

    public ReservationVersionTokenService(
        IReservationVersionRepository reservationVersionRepository,
        IRoomRepository roomRepository)
    {
        this.reservationVersionRepository = reservationVersionRepository;
        this.roomRepository = roomRepository;
    }

    public async Task<GetRoomsVersionTokenResult> GetRoomsTokenAsync(
        DateTimeOffset? start,
        DateTimeOffset? end,
        CancellationToken cancellationToken = default)
    {
        if (!ReservationRequestRules.TryNormalizeRoomsRange(start, end, out var startUtc, out var endUtc, out var failure))
        {
            return new GetRoomsVersionTokenResult.ValidationFailed(failure!);
        }

        if (!startUtc.HasValue)
        {
            return new GetRoomsVersionTokenResult.Success(RoomsNoRangeToken);
        }

        var globalVersion = await reservationVersionRepository.GetGlobalVersionAsync(cancellationToken);
        var normalizedQuery = $"rooms|{ContractVersion}|{startUtc.Value:O}|{endUtc!.Value:O}";
        var token = $"rooms.{ContractVersion}.g{globalVersion}.{ComputeHashPrefix(normalizedQuery)}";
        return new GetRoomsVersionTokenResult.Success(token);
    }

    public async Task<GetRoomReservationsVersionTokenResult> GetRoomReservationsTokenAsync(
        Guid roomId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        if (!ReservationRequestRules.TryNormalizeReservationQuery(roomId, start, end, limit, offset, out var query, out var failure))
        {
            return new GetRoomReservationsVersionTokenResult.ValidationFailed(failure!);
        }

        var roomExists = await roomRepository.ExistsAsync(roomId, cancellationToken);
        if (!roomExists)
        {
            return new GetRoomReservationsVersionTokenResult.NotFound(new NotFoundFailure("room_not_found", "Room was not found."));
        }

        var roomVersion = await reservationVersionRepository.GetRoomVersionAsync(roomId, cancellationToken) ?? 0;
        var normalizedQuery = $"room-reservations|{ContractVersion}|{roomId:D}|{FormatOptionalUtc(query.StartUtc)}|{FormatOptionalUtc(query.EndUtc)}|{query.Limit}|{query.Offset}";
        var token = $"room-reservations.{ContractVersion}.r{roomVersion}.{ComputeHashPrefix(normalizedQuery)}";

        return new GetRoomReservationsVersionTokenResult.Success(token);
    }

    private static string ComputeHashPrefix(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hashBytes = SHA256.HashData(bytes);
        var hashHex = Convert.ToHexStringLower(hashBytes);
        return hashHex[..16];
    }

    private static string FormatOptionalUtc(DateTimeOffset? value)
    {
        return value.HasValue ? value.Value.ToString("O") : "*";
    }
}