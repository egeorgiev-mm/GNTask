namespace RoomBooking.Services.Contracts;

public sealed record ReservationQuery(
    Guid RoomId,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    int Limit,
    int Offset);