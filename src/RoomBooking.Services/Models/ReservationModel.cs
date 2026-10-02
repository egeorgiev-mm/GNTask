namespace RoomBooking.Services.Models;

public sealed record ReservationModel(
    Guid Id,
    Guid RoomId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Title,
    DateTimeOffset CreatedAtUtc);