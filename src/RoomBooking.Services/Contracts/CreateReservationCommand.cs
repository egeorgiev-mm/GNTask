namespace RoomBooking.Services.Contracts;

public sealed record CreateReservationCommand(
    Guid RoomId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Title,
    DateTimeOffset CreatedAtUtc = default);