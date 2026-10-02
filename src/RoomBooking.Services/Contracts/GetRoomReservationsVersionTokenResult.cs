namespace RoomBooking.Services.Contracts;

public abstract record GetRoomReservationsVersionTokenResult
{
    private GetRoomReservationsVersionTokenResult()
    {
    }

    public sealed record Success(string Token) : GetRoomReservationsVersionTokenResult;

    public sealed record ValidationFailed(ValidationFailure Failure) : GetRoomReservationsVersionTokenResult;

    public sealed record NotFound(NotFoundFailure Failure) : GetRoomReservationsVersionTokenResult;
}