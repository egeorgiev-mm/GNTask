using Microsoft.EntityFrameworkCore;
using RoomBooking.Services.Repositories;
using System.Data;

namespace RoomBooking.Data.Repositories;

public sealed class BookingUnitOfWork : IBookingUnitOfWork
{
    private readonly RoomBookingDbContext _db;

    public BookingUnitOfWork(RoomBookingDbContext db) => _db = db;

    public async Task<T> ExecuteInImmediateTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        // For Microsoft.Data.Sqlite, Serializable maps to BEGIN IMMEDIATE.
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var result = await work(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
