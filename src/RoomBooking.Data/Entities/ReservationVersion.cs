using System;

namespace RoomBooking.Data.Entities;

/// <summary>
/// ReservationVersion holds one counter per scope.
/// RoomId==GlobalScope is the global counter; any other RoomId is a room-specific counter.
/// </summary>
public sealed class ReservationVersion
{
    public static readonly Guid GlobalScope = Guid.Empty;

    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public long Counter { get; set; }
}
