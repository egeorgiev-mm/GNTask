using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RoomBooking.Data.Entities;

namespace RoomBooking.Data;

public sealed class RoomBookingDbContext : DbContext
{
    public RoomBookingDbContext(DbContextOptions<RoomBookingDbContext> options) : base(options)
    {
    }

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<ReservationVersion> ReservationVersions => Set<ReservationVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dtoConverter = new ValueConverter<DateTimeOffset, long>(
            v => v.UtcDateTime.Ticks,
            v => new DateTimeOffset(new DateTime(v, DateTimeKind.Utc)));

        modelBuilder.Entity<Room>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(r => r.Name).IsUnique();
        });

        modelBuilder.Entity<Reservation>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.Title).IsRequired().HasMaxLength(200);
            b.Property(r => r.StartUtc).HasConversion(dtoConverter);
            b.Property(r => r.EndUtc).HasConversion(dtoConverter);
            b.Property(r => r.CreatedAtUtc).HasConversion(dtoConverter);
            b.HasOne(r => r.Room).WithMany().HasForeignKey(r => r.RoomId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(r => new { r.RoomId, r.StartUtc, r.EndUtc });
        });

        modelBuilder.Entity<ReservationVersion>(b =>
        {
            b.HasKey(v => v.Id);
            b.HasIndex(v => v.RoomId).IsUnique();
        });
    }
}
