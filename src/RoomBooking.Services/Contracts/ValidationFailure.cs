namespace RoomBooking.Services.Contracts;

public sealed record ValidationFailure(
    string Code,
    string Message,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ErrorsByField);