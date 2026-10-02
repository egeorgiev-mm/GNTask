using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoomBooking.Data.DesignTime;

public sealed class RoomBookingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RoomBookingDbContext>
{
    public RoomBookingDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("RoomBooking__ConnectionString") ?? "Data Source=roombooking.db";
        var options = new DbContextOptionsBuilder<RoomBookingDbContext>().UseSqlite(cs).Options;
        return new RoomBookingDbContext(options);
    }
}
