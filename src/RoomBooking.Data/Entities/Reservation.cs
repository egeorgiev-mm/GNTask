using System;

namespace RoomBooking.Data.Entities;

public sealed class Reservation
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public required string Title { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Room? Room { get; set; }
}
