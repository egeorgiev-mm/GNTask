namespace RoomBooking.Services.Models;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Limit,
    int Offset,
    int TotalCount);