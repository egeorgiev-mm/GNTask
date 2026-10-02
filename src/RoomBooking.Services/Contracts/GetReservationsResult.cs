using RoomBooking.Services.Models;

namespace RoomBooking.Services.Contracts;

public abstract record GetReservationsResult
{
    private GetReservationsResult()
    {
    }

    public sealed record Success(PagedResult<ReservationModel> Reservations) : GetReservationsResult;

    public sealed record ValidationFailed(ValidationFailure Failure) : GetReservationsResult;

    public sealed record NotFound(NotFoundFailure Failure) : GetReservationsResult;
}