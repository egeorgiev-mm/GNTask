namespace RoomBooking.Services.Models;

public sealed record RoomModel(
    Guid Id,
    string Name,
    int Capacity);