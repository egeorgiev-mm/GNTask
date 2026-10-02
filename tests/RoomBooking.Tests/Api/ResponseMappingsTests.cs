using RoomBooking.Api.Mappings;
using RoomBooking.Services.Models;
using Shouldly;

namespace RoomBooking.Tests.Api;

public sealed class ResponseMappingsTests
{
    [Fact]
    public void ReservationMapping_MapsCreatedAtUtcToCreatedAt()
    {
        var createdAtUtc = new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero);
        var model = new ReservationModel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero),
            "Team sync",
            createdAtUtc);

        var response = model.ToResponse();

        response.CreatedAt.ShouldBe(createdAtUtc);
    }
}
