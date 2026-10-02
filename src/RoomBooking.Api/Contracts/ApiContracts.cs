namespace RoomBooking.Api.Contracts;

public sealed record RoomResponse(Guid Id, string Name, int Capacity);

public sealed record ReservationResponse(
    Guid Id,
    Guid RoomId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Title,
    DateTimeOffset CreatedAt);

public sealed record CreateReservationRequest(DateTimeOffset? Start, DateTimeOffset? End, string? Title);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Limit, int Offset, int Total, bool HasMore);
