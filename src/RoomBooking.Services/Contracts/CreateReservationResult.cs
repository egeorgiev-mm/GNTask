using RoomBooking.Services.Models;

namespace RoomBooking.Services.Contracts;

public abstract record CreateReservationResult
{
    private CreateReservationResult()
    {
    }

    public sealed record Success(ReservationModel Reservation) : CreateReservationResult;

    public sealed record ValidationFailed(ValidationFailure Failure) : CreateReservationResult;

    public sealed record NotFound(NotFoundFailure Failure) : CreateReservationResult;

    public sealed record Conflict(ConflictFailure Failure) : CreateReservationResult;
}