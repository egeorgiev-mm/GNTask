using RoomBooking.Services.Models;

namespace RoomBooking.Services.Contracts;

public abstract record GetRoomsResult
{
    private GetRoomsResult()
    {
    }

    public sealed record Success(IReadOnlyList<RoomModel> Rooms) : GetRoomsResult;

    public sealed record ValidationFailed(ValidationFailure Failure) : GetRoomsResult;
}