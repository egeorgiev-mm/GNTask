namespace RoomBooking.Services.Repositories;

public interface IBookingUnitOfWork
{
    Task<T> ExecuteInImmediateTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default);
}