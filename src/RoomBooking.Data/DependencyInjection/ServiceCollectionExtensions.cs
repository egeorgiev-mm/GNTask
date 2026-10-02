using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using RoomBooking.Data.Repositories;
using RoomBooking.Services.Repositories;

namespace RoomBooking.Data.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoomBookingData(this IServiceCollection services, string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);

        // Microsoft.Data.Sqlite applies DefaultTimeout as the connection busy timeout.
        var hasExplicitTimeout = connectionString.Contains("Default Timeout=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Command Timeout=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("DefaultTimeout=", StringComparison.OrdinalIgnoreCase);
        if (!hasExplicitTimeout)
        {
            builder.DefaultTimeout = 30;
        }

        builder.ForeignKeys = true;

        services.AddDbContext<RoomBookingDbContext>(opts =>
        {
            opts.UseSqlite(builder.ToString(), b => b.MigrationsAssembly("RoomBooking.Data"));
        });

        services.AddScoped<RoomRepository>();
        services.AddScoped<ReservationRepository>();
        services.AddScoped<ReservationVersionRepository>();
        services.AddScoped<BookingUnitOfWork>();
        services.AddScoped<RoomBookingDbInitializer>();

        // Register service interfaces to concrete types for Api wiring later
        services.AddScoped<IRoomRepository>(sp => sp.GetRequiredService<RoomRepository>());
        services.AddScoped<IReservationRepository>(sp => sp.GetRequiredService<ReservationRepository>());
        services.AddScoped<IReservationVersionRepository>(sp => sp.GetRequiredService<ReservationVersionRepository>());
        services.AddScoped<IBookingUnitOfWork>(sp => sp.GetRequiredService<BookingUnitOfWork>());

        return services;
    }
}
