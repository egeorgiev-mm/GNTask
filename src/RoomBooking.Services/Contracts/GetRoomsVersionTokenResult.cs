namespace RoomBooking.Services.Contracts;

public abstract record GetRoomsVersionTokenResult
{
    private GetRoomsVersionTokenResult()
    {
    }

    public sealed record Success(string Token) : GetRoomsVersionTokenResult;

    public sealed record ValidationFailed(ValidationFailure Failure) : GetRoomsVersionTokenResult;
}