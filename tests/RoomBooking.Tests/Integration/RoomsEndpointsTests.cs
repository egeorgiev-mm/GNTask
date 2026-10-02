using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using RoomBooking.Api.Contracts;
using RoomBooking.Tests.Integration.TestInfrastructure;
using Shouldly;

namespace RoomBooking.Tests.Integration;

public sealed class RoomsEndpointsTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;

    public RoomsEndpointsTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"roombooking_{Guid.NewGuid():N}.db");
        _factory = new TestAppFactory(_dbPath);
        _client = _factory.CreateClient();
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();

        SqliteConnection.ClearAllPools();
        DeleteIfExists(_dbPath);
        DeleteIfExists(_dbPath + "-wal");
        DeleteIfExists(_dbPath + "-shm");

        return ValueTask.CompletedTask;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetRooms_UsesPerTestTempDatabase_AndReturnsSeededRooms()
    {
        // Arrange

        // Act
        var response = await _client.GetAsync("/rooms", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();

        File.Exists(_dbPath).ShouldBeTrue();
        File.Exists(Path.Combine(AppContext.BaseDirectory, "roombooking.db")).ShouldBeFalse();
        File.Exists(Path.Combine(AppContext.BaseDirectory, "roombooking.db-wal")).ShouldBeFalse();
        File.Exists(Path.Combine(AppContext.BaseDirectory, "roombooking.db-shm")).ShouldBeFalse();

        var rooms = await response.Content.ReadFromJsonAsync<List<RoomResponse>>(TestContext.Current.CancellationToken);
        rooms.ShouldNotBeNull();
        rooms!.Count.ShouldBe(3);
        rooms.Select(r => r.Name).ShouldBe(["Auditorium", "Conference Room", "Small Room"]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetRooms_ConditionalGet_Returns304WithEmptyBody()
    {
        // Arrange
        var first = await _client.GetAsync("/rooms", TestContext.Current.CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var req = new HttpRequestMessage(HttpMethod.Get, "/rooms");
        req.Headers.IfNoneMatch.Add(first.Headers.ETag!);

        // Act
        var second = await _client.SendAsync(req, TestContext.Current.CancellationToken);

        // Assert
        second.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        (await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        second.Headers.ETag.ShouldBe(first.Headers.ETag);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetRooms_StartWithoutEnd_Returns400Validation()
    {
        // Arrange

        // Act
        var response = await _client.GetAsync("/rooms?start=2026-10-03T09:00:00Z", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("invalid_time_range");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}