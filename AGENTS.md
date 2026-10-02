# AGENTS

## Purpose and Constraints
- This repository is a small room-booking take-home API.
- Keep changes minimal and practical; avoid over-engineering.
- By decision, do not introduce shared build-settings props/targets files just for this project.

## Build, Test, Run, Migrations

```bash
# Restore + build
dotnet restore GNTask.slnx
dotnet build GNTask.slnx

# Run API
dotnet run --project src/RoomBooking.Api

# Run tests (SDK 10 + Microsoft.Testing.Platform)
dotnet test --solution GNTask.slnx

# Example filtered runs
dotnet test --solution GNTask.slnx --filter "Category=Integration"
dotnet test --solution GNTask.slnx --filter "FullyQualifiedName~RoomBooking.Tests.Services"
```

EF tooling (local tool manifest at `.config/dotnet-tools.json`):

```bash
dotnet tool restore

# Add migration
dotnet ef migrations add <MigrationName> \
  --project src/RoomBooking.Data \
  --startup-project src/RoomBooking.Api

# Apply migration
dotnet ef database update \
  --project src/RoomBooking.Data \
  --startup-project src/RoomBooking.Api
```

## Project Map and Dependency Rules
- `src/RoomBooking.Api`: minimal API, DTO contracts, ProblemDetails mapping, Autofac composition root.
- `src/RoomBooking.Data`: EF Core entities/DbContext/migrations, repository implementations.
- `src/RoomBooking.Services`: business rules, contracts, repository/service interfaces.
- `tests/RoomBooking.Tests`: unit, repository, and integration tests.

Rules:
- Services must not reference EF Core or Autofac.
- Autofac is allowed only in API composition root.
- EF entities must never leave the Data layer; expose service models/contracts instead.

## Conventions
- Use service result objects (success/failure records), not exceptions, for expected domain outcomes.
- Keep ProblemDetails mapping centralized in API infrastructure.
- API DTOs belong only in API project.
- Normalize incoming times to UTC in Services before persistence/querying.
- Keep repositories thin (query/persistence only, no business policy).

When adding functionality:
1. Add/extend service contract in Services.
2. Implement/extend repository interface in Services and implementation in Data.
3. Implement business logic in Services.
4. Add endpoint + API DTO mapping in API.
5. Register dependencies:
   - Services in `src/RoomBooking.Api/RoomBookingModule.cs`
   - Data interfaces/implementations in `src/RoomBooking.Data/DependencyInjection/ServiceCollectionExtensions.cs`

## Database Conventions
- Use EF migrations for schema evolution.
- Do not use `EnsureCreated` in production code paths.
- SQLite stores `DateTimeOffset` values as UTC ticks (`INTEGER`) for ordering/range correctness.
- `ReservationVersions` uses sentinel scope `RoomId = Guid.Empty` for the global counter; room counters use concrete room IDs.
- Maintain indexes deliberately:
  - Unique room name
  - Composite reservation index `(RoomId, StartUtc, EndUtc)`

## API Behavior Notes
- Seeded room IDs are fixed and stable:
  - Small Room (4): `11111111-1111-1111-1111-111111111111`
  - Conference Room (10): `22222222-2222-2222-2222-222222222222`
  - Auditorium (50): `33333333-3333-3333-3333-333333333333`
- Reservation range filtering uses overlap semantics: `StartUtc < end && EndUtc > start`.
- Validation codes include `start_required`, `end_required`, and `title_required` for missing POST fields.
- ETag validation intentionally happens before endpoint execution for conditional GET (by design).

## Testing Conventions
- Framework/tools: xUnit v3 + Microsoft.Testing.Platform, NSubstitute, Shouldly.
- Test naming pattern: `Method_State_ExpectedBehavior`.
- Structure tests with Arrange/Act/Assert.
- Repository/integration tests should use temp-file SQLite databases.
- Pass `TestContext.Current.CancellationToken` to async APIs.

## Non-goals (Current Scope)
- Enterprise-scale architecture, distributed components, or unnecessary abstraction layers.
- Premature optimization beyond demonstrated hot paths.
- Feature-complete production concerns (auth, rate limiting, CI/CD, advanced observability, etc.) unless explicitly requested.