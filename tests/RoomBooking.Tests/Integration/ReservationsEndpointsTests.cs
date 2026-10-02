using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RoomBooking.Api.Contracts;
using RoomBooking.Tests.Integration.TestInfrastructure;
using Shouldly;

namespace RoomBooking.Tests.Integration;

public sealed class ReservationsEndpointsTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;

    public ReservationsEndpointsTests()
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
    public async Task PostReservation_MissingStart_Returns400WithStableCode()
    {
        var roomId = await GetAnyRoomIdAsync();
        var payload = """
        {
          "end": "2026-10-03T10:00:00Z",
          "title": "Engineering Standup"
        }
        """;

        var response = await _client.PostAsync($"/rooms/{roomId}/reservations", new StringContent(payload, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("code").GetString().ShouldBe("start_required");
        json.RootElement.GetProperty("errors").GetProperty("start")[0].GetString().ShouldBe("Start is required.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostReservation_MissingEnd_Returns400WithStableCode()
    {
        var roomId = await GetAnyRoomIdAsync();
        var payload = """
        {
          "start": "2026-10-03T09:00:00Z",
          "title": "Engineering Standup"
        }
        """;

        var response = await _client.PostAsync($"/rooms/{roomId}/reservations", new StringContent(payload, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("code").GetString().ShouldBe("end_required");
        json.RootElement.GetProperty("errors").GetProperty("end")[0].GetString().ShouldBe("End is required.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostReservation_MissingTitle_Returns400WithStableCode()
    {
        var roomId = await GetAnyRoomIdAsync();
        var payload = """
        {
          "start": "2026-10-03T09:00:00Z",
          "end": "2026-10-03T10:00:00Z"
        }
        """;

        var response = await _client.PostAsync($"/rooms/{roomId}/reservations", new StringContent(payload, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("code").GetString().ShouldBe("title_required");
        json.RootElement.GetProperty("errors").GetProperty("title")[0].GetString().ShouldBe("Title is required.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostReservation_InvalidRangeAndEmptyTitle_Return400()
    {
        var roomId = await GetAnyRoomIdAsync();

        var invalidRange = new CreateReservationRequest(
            new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero),
            "x");

        var invalidRangeResponse = await _client.PostAsJsonAsync($"/rooms/{roomId}/reservations", invalidRange, TestContext.Current.CancellationToken);
        invalidRangeResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await invalidRangeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("invalid_time_range");

        var emptyTitle = new CreateReservationRequest(
            new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero),
            "   ");

        var emptyTitleResponse = await _client.PostAsJsonAsync($"/rooms/{roomId}/reservations", emptyTitle, TestContext.Current.CancellationToken);
        emptyTitleResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await emptyTitleResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("title_required");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostReservation_MalformedJson_Returns400BadRequestCode()
    {
        var roomId = await GetAnyRoomIdAsync();
        var malformed = "{ \"start\": \"2026-10-03T09:00:00Z\", \"end\": }";

        var response = await _client.PostAsync($"/rooms/{roomId}/reservations", new StringContent(malformed, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("bad_request");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostReservation_UnknownRoom_Returns404()
    {
        var request = new CreateReservationRequest(
            new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero),
            "Missing room");

        var response = await _client.PostAsJsonAsync($"/rooms/{Guid.NewGuid()}/reservations", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("room_not_found");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetReservations_DefaultLimitOrderAndPagingMetadata()
    {
        var roomId = await GetAnyRoomIdAsync();

        for (var i = 0; i < 51; i++)
        {
            var start = new DateTimeOffset(2026, 10, 10, 8, 0, 0, TimeSpan.Zero).AddMinutes(i);
            var payload = new CreateReservationRequest(start, start.AddMinutes(1), $"item-{i:00}");
            var post = await _client.PostAsJsonAsync($"/rooms/{roomId}/reservations", payload, TestContext.Current.CancellationToken);
            post.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var response = await _client.GetAsync($"/rooms/{roomId}/reservations", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ReservationResponse>>(TestContext.Current.CancellationToken);
        page.ShouldNotBeNull();
        page!.Limit.ShouldBe(50);
        page.Offset.ShouldBe(0);
        page.Total.ShouldBe(51);
        page.HasMore.ShouldBeTrue();

        var starts = page.Items.Select(x => x.Start).ToList();
        starts.ShouldBe(starts.OrderBy(x => x).ToList());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetReservations_RangeUsesOverlapSemantics()
    {
        var roomId = await GetAnyRoomIdAsync();

        var first = new CreateReservationRequest(
            new DateTimeOffset(2026, 10, 3, 9, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 3, 10, 30, 0, TimeSpan.Zero),
            "overlap");

        var second = new CreateReservationRequest(
            new DateTimeOffset(2026, 10, 3, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero),
            "outside");

        (await _client.PostAsJsonAsync($"/rooms/{roomId}/reservations", first, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await _client.PostAsJsonAsync($"/rooms/{roomId}/reservations", second, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await _client.GetAsync($"/rooms/{roomId}/reservations?start=2026-10-03T10:00:00Z&end=2026-10-03T11:00:00Z", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ReservationResponse>>(TestContext.Current.CancellationToken);
        page.ShouldNotBeNull();
        page!.Items.Count.ShouldBe(1);
        page.Items[0].Title.ShouldBe("overlap");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetReservations_ConditionalGet_304AndETagChangesOnlyForRoomChanges()
    {
        var rooms = await GetRoomsAsync();
        var roomA = rooms[0].Id;
        var roomB = rooms[1].Id;

        var first = await _client.GetAsync($"/rooms/{roomA}/reservations", TestContext.Current.CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var etagA = first.Headers.ETag;
        etagA.ShouldNotBeNull();

        var ifNoneMatch = new HttpRequestMessage(HttpMethod.Get, $"/rooms/{roomA}/reservations");
        ifNoneMatch.Headers.IfNoneMatch.Add(etagA!);
        var notModified = await _client.SendAsync(ifNoneMatch, TestContext.Current.CancellationToken);
        notModified.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        (await notModified.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();

        var createInOtherRoom = await _client.PostAsJsonAsync(
            $"/rooms/{roomB}/reservations",
            new CreateReservationRequest(
                new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 4, 10, 0, 0, TimeSpan.Zero),
                "other-room"),
            TestContext.Current.CancellationToken);
        createInOtherRoom.StatusCode.ShouldBe(HttpStatusCode.Created);

        var unchangedReq = new HttpRequestMessage(HttpMethod.Get, $"/rooms/{roomA}/reservations");
        unchangedReq.Headers.IfNoneMatch.Add(etagA!);
        var stillNotModified = await _client.SendAsync(unchangedReq, TestContext.Current.CancellationToken);
        stillNotModified.StatusCode.ShouldBe(HttpStatusCode.NotModified);

        var createInRoomA = await _client.PostAsJsonAsync(
            $"/rooms/{roomA}/reservations",
            new CreateReservationRequest(
                new DateTimeOffset(2026, 10, 4, 11, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero),
                "same-room"),
            TestContext.Current.CancellationToken);
        createInRoomA.StatusCode.ShouldBe(HttpStatusCode.Created);

        var changedReq = new HttpRequestMessage(HttpMethod.Get, $"/rooms/{roomA}/reservations");
        changedReq.Headers.IfNoneMatch.Add(etagA!);
        var changed = await _client.SendAsync(changedReq, TestContext.Current.CancellationToken);
        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        changed.Headers.ETag.ShouldNotBeNull();
        changed.Headers.ETag!.Tag.ShouldNotBe(etagA!.Tag);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetReservations_MalformedLimit_Returns400AndMentionsParameter()
    {
        var roomId = await GetAnyRoomIdAsync();
        var response = await _client.GetAsync($"/rooms/{roomId}/reservations?limit=abc", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("bad_request");
        body.ShouldContain("limit");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostReservation_OverlappingConflict_AndBackToBackAllowed()
    {
        var roomId = await GetAnyRoomIdAsync();

        var first = new CreateReservationRequest(
            new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero),
            "first");

        var firstResponse = await _client.PostAsJsonAsync($"/rooms/{roomId}/reservations", first, TestContext.Current.CancellationToken);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var overlapping = new CreateReservationRequest(
            new DateTimeOffset(2026, 10, 5, 10, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 5, 11, 30, 0, TimeSpan.Zero),
            "overlap");

        var conflict = await _client.PostAsJsonAsync($"/rooms/{roomId}/reservations", overlapping, TestContext.Current.CancellationToken);
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await conflict.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("reservation_conflict");

        var backToBack = new CreateReservationRequest(
            new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero),
            "back-to-back");

        var backToBackResponse = await _client.PostAsJsonAsync($"/rooms/{roomId}/reservations", backToBack, TestContext.Current.CancellationToken);
        backToBackResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Concurrency_ParallelOverlappingRequests_OneCreatedRestConflict_NoServerErrors()
    {
        var roomId = await GetAnyRoomIdAsync();

        var start = new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 2, 11, 0, 0, TimeSpan.Zero);

        var tasks = Enumerable.Range(0, 8)
            .Select(i => _client.PostAsJsonAsync(
                $"/rooms/{roomId}/reservations",
                new CreateReservationRequest(start, end, $"Concurrent {i}"),
                TestContext.Current.CancellationToken))
            .ToArray();

        var responses = await Task.WhenAll(tasks);
        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(7);
        responses.Count(r => (int)r.StatusCode >= 500).ShouldBe(0);

        File.Exists(_dbPath).ShouldBeTrue();
        File.Exists(Path.Combine(AppContext.BaseDirectory, "roombooking.db")).ShouldBeFalse();
    }

    private async Task<Guid> GetAnyRoomIdAsync()
    {
        var rooms = await GetRoomsAsync();
        return rooms[0].Id;
    }

    private async Task<List<RoomResponse>> GetRoomsAsync()
    {
        var response = await _client.GetAsync("/rooms", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var rooms = await response.Content.ReadFromJsonAsync<List<RoomResponse>>(TestContext.Current.CancellationToken);
        rooms.ShouldNotBeNull();
        return rooms!;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(payload);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}