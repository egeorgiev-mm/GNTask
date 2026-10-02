---
last_updated: 2026-03-10
purpose: "Durable project decisions and invariants. Template file for downstream projects."
---

# Project Decisions

## How to Use

- Add entries only when a decision is durable and likely to matter in future sessions.
- Prefer linking to code/paths and stating invariants/constraints over narrative.
- If a decision is superseded, append an "Update" note to the original entry.
- Keep runtime scratch notes out of this file.
- Separate verified repo facts from assumptions or interpretations.

## Entry Template

```md
## <Decision Title> — YYYY-MM-DD

### Facts
- Verified repo facts with file/path references.

### Inferences
- Assumptions or interpretations that still need validation.

### Decision
- The durable rule, invariant, or operating choice.

### Consequences
- What this changes, constrains, or requires going forward.
```

## Onboarding Snapshot Template

Use this after project familiarization / onboarding runs:

```md
## Onboarding Snapshot — YYYY-MM-DD

### Facts
- Major modules / packages
- Run / build / test commands
- Key conventions and invariants
- Top risks or TODOs worth remembering

### Inferences
- Only if necessary, clearly marked
```

## Entries

<!-- Add project-specific durable decisions below. Keep this file empty in reusable template repositories. -->
 
 -_No project-specific decisions recorded yet._

## Room Booking API Baseline — 2026-10-01

### Facts
- Repo projects: `src/RoomBooking.Api`, `src/RoomBooking.Services`, `src/RoomBooking.Data`, `tests/RoomBooking.Tests`.
- Minimal API in `src/RoomBooking.Api` with Autofac composition root and DTOs. ProblemDetails mapping and conditional GET (ETag/If-None-Match) live in API project.
- `src/RoomBooking.Services` holds business logic, service contracts, `IBookingUnitOfWork`, `IClock`, result types; it must not reference EF Core or Autofac.
- `src/RoomBooking.Data` contains EF Core SQLite entities, `DbContext`, repositories, `BookingUnitOfWork`, initializer, and migrations.
- Tests use xUnit v3 + Microsoft.Testing.Platform (MTP), NSubstitute, Shouldly; `dotnet test --solution GNTask.slnx` (SDK 10 + MTP) is the standard command.
- Dependency direction: Api -> Data -> Services. No shared Directory.Build.props or Directory.Packages.props by project decision; `global.json` selects MTP runner only.

### Inferences
- These invariants are relied on by tests and composition rules (e.g., Autofac only in API).

### Decision
- Preserve the three-project layout and dependency directions; keep migrations in `src/RoomBooking.Data`; avoid adding repo-wide build props or packaging props unless explicitly requested.
- Normalize incoming times to UTC in Services; persist `DateTimeOffset` as UTC ticks in SQLite.
- Use half-open reservation semantics: overlap when existing.Start < reqEnd && existing.End > reqStart.

### Consequences
- Service layer must perform UTC normalization before persistence/querying.
- Migrations and EF concerns remain isolated to Data project; Services remain free of EF/Autofac references.

### Citations
- [AGENTS.md](AGENTS.md)
- [src/RoomBooking.Api](src/RoomBooking.Api)
- [src/RoomBooking.Services](src/RoomBooking.Services)
- [src/RoomBooking.Data](src/RoomBooking.Data)

### memory_meta
- timestamp: 2026-10-01
- author: GitHub Copilot
