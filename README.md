# GNTask

## Overview
Small .NET 10 room-booking take-home API with three layers:
- API: HTTP endpoints, transport DTOs, ProblemDetails mapping, ETag handling.
- Data: EF Core + SQLite persistence and repositories.
- Services: business rules, validation, and service contracts.

## Prerequisites
- .NET 10 SDK

No additional setup is required.

## Quick Start
1. Restore and build:

```bash
dotnet restore GNTask.slnx
dotnet build GNTask.slnx
```

2. Run the API:

```bash
dotnet run --project src/RoomBooking.Api
```

3. Database behavior:
- Default connection string is `ConnectionStrings:RoomBooking = Data Source=roombooking.db`.
- The SQLite file is created from that data source path (relative to the app process working directory unless you provide an absolute path).
- You can change it in `src/RoomBooking.Api/appsettings.json` or via configuration override for `ConnectionStrings:RoomBooking`.

4. Startup initialization:
- EF migrations are applied at startup.
- Seed data is inserted idempotently (3 rooms + global reservation version row).

Seeded room IDs are fixed:
- `Small Room` (capacity `4`): `11111111-1111-1111-1111-111111111111`
- `Conference Room` (capacity `10`): `22222222-2222-2222-2222-222222222222`
- `Auditorium` (capacity `50`): `33333333-3333-3333-3333-333333333333`

5. Manual API calls:
- Use `src/RoomBooking.Api/RoomBooking.Api.http`.

6. OpenAPI:
- Enabled only in Development (`app.MapOpenApi()` inside `if (app.Environment.IsDevelopment())`).
- Default document endpoint: `/openapi/v1.json`.

7. Run tests:

```bash
dotnet test --solution GNTask.slnx
```

8. Filtered test runs (SDK 10 + Microsoft.Testing.Platform):

```bash
# Run integration tests (xUnit Trait: Category=Integration)
dotnet test --solution GNTask.slnx --filter "Category=Integration"

# Run a specific test class
dotnet test --solution GNTask.slnx --filter "FullyQualifiedName~RoomBooking.Tests.Services.RoomBookingServiceTests"
```

## API Reference

### `GET /rooms`
Query parameters:
- `start` (optional, must be paired with `end`)
- `end` (optional, must be paired with `start`)

Behavior:
- No range: returns all rooms.
- With range: returns rooms with no overlapping reservation for that window.

Status codes:
- `200 OK`
- `304 Not Modified` (conditional GET with matching `If-None-Match`)
- `400 Bad Request` (validation/binding)

Example request:

```http
GET /rooms?start=2026-10-03T09:00:00Z&end=2026-10-03T10:00:00Z
```

Example response (`200`):

```json
[
	{
		"id": "5f1c7b0e-c38a-4c26-9f6a-1b9b95ef2d45",
		"name": "Conference Room",
		"capacity": 10
	}
]
```

### `GET /rooms/{roomId}/reservations`
Query parameters:
- `start` (optional)
- `end` (optional)
- `limit` (optional, default `50`, min `1`, max `100`)
- `offset` (optional, default `0`, min `0`)

Filtering semantics:
- If both `start` and `end` are provided, reservations are filtered by overlap (`existing.start < end && existing.end > start`).
- If only `start` is provided, reservations are filtered by `existing.end > start`.
- If only `end` is provided, reservations are filtered by `existing.start < end`.

Status codes:
- `200 OK`
- `304 Not Modified` (conditional GET with matching `If-None-Match`)
- `400 Bad Request` (validation/binding)
- `404 Not Found` (`room_not_found`)

Example request:

```http
GET /rooms/5f1c7b0e-c38a-4c26-9f6a-1b9b95ef2d45/reservations?start=2026-10-03T00:00:00Z&end=2026-10-04T00:00:00Z&limit=25&offset=0
```

Example response (`200`):

```json
{
	"items": [
		{
			"id": "0f32d2d4-53d7-4d22-b16a-c0e0cf8a0b95",
			"roomId": "5f1c7b0e-c38a-4c26-9f6a-1b9b95ef2d45",
			"start": "2026-10-03T09:00:00+00:00",
			"end": "2026-10-03T10:00:00+00:00",
			"title": "Engineering Standup",
			"createdAt": "2026-10-01T08:00:00+00:00"
		}
	],
	"limit": 25,
	"offset": 0,
	"total": 1,
	"hasMore": false
}
```

### `POST /rooms/{roomId}/reservations`
Request body:

```json
{
	"start": "2026-10-03T09:00:00Z",
	"end": "2026-10-03T10:00:00Z",
	"title": "Engineering Standup"
}
```

Status codes:
- `201 Created`
- `400 Bad Request` (validation/binding)
- `404 Not Found` (`room_not_found`)
- `409 Conflict` (`reservation_conflict`)

Example response (`201`):

```json
{
	"id": "0f32d2d4-53d7-4d22-b16a-c0e0cf8a0b95",
	"roomId": "5f1c7b0e-c38a-4c26-9f6a-1b9b95ef2d45",
	"start": "2026-10-03T09:00:00+00:00",
	"end": "2026-10-03T10:00:00+00:00",
	"title": "Engineering Standup",
	"createdAt": "2026-10-01T08:00:00+00:00"
}
```

### ProblemDetails shape and stable `code` values

Validation failures use `ValidationProblemDetails` with field errors:

```json
{
	"type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
	"title": "Validation failed",
	"status": 400,
	"detail": "Start must be before end.",
	"errors": {
		"start": ["Start must be before end."],
		"end": ["End must be after start."]
	},
	"code": "invalid_time_range"
}
```

Stable `code` values used by the API:
- `invalid_time_range`
- `invalid_pagination`
- `start_required`
- `end_required`
- `title_required`
- `title_too_long`
- `room_not_found`
- `reservation_conflict`
- `bad_request`
- `internal_error`

### Conditional GET / ETag behavior
- Both list endpoints (`GET /rooms`, `GET /rooms/{roomId}/reservations`) support weak ETags.
- Response headers on `200`/`304`:
	- `ETag: W/"..."`
	- `Cache-Control: no-cache`
- If `If-None-Match` matches current token (including wildcard `*`), API returns `304` before endpoint handler execution.

### Version counters table
- `ReservationVersions` uses a non-null `RoomId` key for scope.
- Sentinel scope row: `RoomId = Guid.Empty` (`ReservationVersion.GlobalScope`) stores the global counter.
- Per-room counters use each room's actual `RoomId`.

## Architecture

### Project map
- `src/RoomBooking.Api`
	- Minimal API endpoints, transport contracts, ProblemDetails mapping, conditional GET filter.
	- Composition root with Autofac module registration.
- `src/RoomBooking.Data`
	- EF Core DbContext, entities, migrations, repository implementations, unit-of-work transaction boundary.
	- Registers repository interfaces for Services.
- `src/RoomBooking.Services`
	- Business rules and service contracts/interfaces.
	- No EF Core or Autofac dependency.
- `tests/RoomBooking.Tests`
	- Unit tests (services/token/utility mapping), repository tests against real SQLite, API integration tests.

### Layer responsibilities and tradeoff
- Services handle validation, UTC normalization, business decisions.
- Repositories are intentionally thin EF wrappers for explicit data access seams and testability.
- Tradeoff: direct `DbContext` in services would be simpler.

## Key Decisions

### Time-window semantics
Reservations are half-open intervals: `[Start, End)`.

Overlap predicate used in code:

```text
existing.StartUtc < requestedEndUtc && existing.EndUtc > requestedStartUtc
```

Examples:
- Existing `10:00-11:00`, requested `10:30-11:30` -> rejected (overlap).
- Existing `10:00-11:00`, requested `11:00-12:00` -> allowed (back-to-back).

### UTC normalization
- Service layer normalizes incoming `DateTimeOffset` values to UTC before querying/saving.
- API responses map times to UTC values.

### Past reservations
- Past dates are allowed; no rule rejects them.

### `GET /rooms` availability meaning
- A room is considered available if no reservation overlaps the requested window.

### Validation rules
- `start` and `end` for room availability must be provided together.
- For reservations, `start < end` is required.
- `title` is required (trimmed, non-empty).
- `title` max length is `200`.
- Pagination: `offset >= 0`, `limit >= 1`, and `limit` is clamped to `100`.

## Performance and Concurrency Decisions
- Composite index on reservations: `(RoomId, StartUtc, EndUtc)` to support overlap and range filtering.
- Unique room name index for integrity and quick lookup.
- `DateTimeOffset` values are stored as UTC ticks (`INTEGER`) in SQLite to preserve stable ordering/range comparisons.
- Availability query uses `NOT EXISTS` (`!Any`) anti-join pattern.
- Read queries use `AsNoTracking()` and project directly to service models.
- Pagination defaults/bounds are enforced centrally in service rules.
- SQLite settings:
	- WAL mode (`PRAGMA journal_mode = WAL;` at initialization).
	- Busy timeout via connection string default (`DefaultTimeout = 30` if not provided).
- Concurrency control for reservation creation:
	- `BEGIN IMMEDIATE` semantics via serializable transaction in `BookingUnitOfWork`.
	- Conflict check + insert + version bump run in one transaction.
	- This prevents concurrent double-booking writes.
- ETag strategy:
	- Weak ETags are built from persisted version counters (global/per-room) + normalized query hash.
	- Not payload hash: avoids full payload materialization for token generation and keeps token generation cheap.
	- Performance notes: token generation performs separate lightweight validation/query-shape work before handler execution by design.
	- `304` can be returned before query handler execution when `If-None-Match` matches.
	- Under races, a token can lag payload freshness slightly, which causes harmless client revalidation, not stale write acceptance.
- Response caching directive is intentionally `Cache-Control: no-cache`.
- Deliberately skipped in this take-home: response compression, output caching, DB trigger/exclusion constraint enforcement, `If-Match` write preconditions (no update endpoint), single-reservation `GET` endpoint.

## Testing Strategy
- Service unit tests:
	- `NSubstitute` for collaborators.
	- `Shouldly` assertions.
	- Focus on validation, overlap outcomes, UTC normalization, pagination clamping, and version bump behavior.
- Repository tests:
	- Real temp-file SQLite DB.
	- Verify overlap predicate, availability filtering, sorting/pagination behavior, version increments, UTC ordering, and concurrency behavior.
- API integration tests:
	- `WebApplicationFactory<Program>`.
	- In-memory configuration for test connection string.
	- Verify endpoint status codes, payloads, ETag behavior, and concurrent booking behavior.

Run commands:

```bash
# All tests
dotnet test --solution GNTask.slnx

## What Is Missing / Production Next Steps
- Authentication and authorization.
- Reservation cancel/update/delete endpoints with optimistic concurrency (`If-Match`/`412 Precondition Failed`).
- Recurring bookings.
- Keyset pagination for large datasets.
