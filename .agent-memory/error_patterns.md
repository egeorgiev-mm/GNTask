---
last_updated: 2026-03-10
purpose: "Recurring bug patterns and fixes. Template file for downstream projects."
---

# Error Patterns

## How to Use

- Record only repeatable patterns (root cause + fix + prevention).
- Include reproduction signal when available (test name, stack trace snippet, command).
- Prefer actionable prevention guardrails (lint rule, test, invariant, CI gate).
- Keep entries short and reusable; do not dump incident timelines.

## Entry Template

```md
## <Pattern Title> — YYYY-MM-DD

### Reproduction Signal
- Test name, stack trace, failing command, or clear repro steps.

### Root Cause
- The repeated failure mode.

### Fix
- What resolved it.

### Prevention
- Guardrail: test, lint, invariant, review rule, or coding constraint.
```

## Patterns

<!-- Add project-specific recurring patterns below. Keep this file empty in reusable template repositories. -->

_No patterns recorded yet._

## WebApplicationFactory: connection-string override required — 2026-10-01

### Reproduction Signal
- Integration tests share a DB or fail when test isolation not set; TestServer/Factory uses `Program` configuration that reads connection string eagerly.

### Root Cause
- `Program` reads connection string early; overriding configuration via `ConfigureAppConfiguration` is too late for some initialization paths.

### Fix
- In tests use `builder.UseSetting("ConnectionStrings:Default", "...")` (or equivalent `builder.UseSetting`) to override before `Program` reads settings.

### Prevention
- Test templates and docs: always override connection strings with `builder.UseSetting(...)` in tests that create temp SQLite files.

### Citations
- [tests/RoomBooking.Tests](tests/RoomBooking.Tests)

### memory_meta
- timestamp: 2026-10-01
- author: GitHub Copilot

## SQLite file lifecycle & datetime quirks — 2026-10-01

### Reproduction Signal
- Intermittent unique-index violations, flaky deletes of temp DB files, or incorrect ordering when DateTimeOffset compared as text.

### Root Cause
- SQLite unique indexes allow multiple NULLs; DateTimeOffset text comparisons are unsafe; WAL requires -wal/-shm removal and busy connections block file deletes.

### Fix
- Store `DateTimeOffset` as UTC ticks (INTEGER) via value converter; set `busy_timeout` in connection string; use WAL and clear pools + remove -wal/-shm before deleting files.

### Prevention
- Use consistent UTC ticks converter for DateTimeOffset; set `busy_timeout` in test connection strings; Document WAL and deletion steps in test infra.

### Citations
- [src/RoomBooking.Data]

### memory_meta
- timestamp: 2026-10-01
- author: GitHub Copilot

## Minimal API DTO non-nullable default pitfalls — 2026-10-01

### Reproduction Signal
- DTO binding yields default values for non-nullable value types and validation misses required fields.

### Root Cause
- Minimal API model-binding will give default(T) for missing value-type fields; absent validation allows silent acceptance.

### Fix
- Use nullable DTO fields and explicit validation rules; tests should assert validation error codes (e.g., `start_required`).

### Prevention
- Enforce DTOs with nullable primitives + validation middleware; add tests that assert validation codes and ModelState behavior.

### memory_meta
- timestamp: 2026-10-01
- author: GitHub Copilot

## 304 responses in TestServer are empty-body — 2026-10-01

### Reproduction Signal
- Tests asserting 304 responses see `Content-Length: 0` and empty body.

### Root Cause
- TestServer/TestHost returns 304 with no body; test expectations that assume a payload fail.

### Fix
- Assert empty body for 304 responses in tests (e.g., check `Content-Length` or response body length).

### Prevention
- Add assertion patterns in integration test helpers for conditional GET scenarios.

### memory_meta
- timestamp: 2026-10-01
- author: GitHub Copilot

## dotnet test / MTP runner usage & parallel agent notes — 2026-10-01

### Reproduction Signal
- CI or local runs failing due to incorrect `dotnet test` syntax for SDK 10 and MTP; parallel test agents share state unexpectedly.

### Root Cause
- SDK 10 changed test runner semantics and MTP integration; running parallel agents/projects without agreed contracts leads to contract drift.

### Fix
- Use `dotnet test --solution GNTask.slnx` with proper MTP flags as documented; ensure tests override runtime settings to isolate DBs.

### Prevention
- Document the exact test invocation in onboarding snapshot; require test templates to set override settings for per-test DB isolation.

### memory_meta
- timestamp: 2026-10-01
- author: GitHub Copilot

## Subagent / long-run output truncation guardrail — 2026-10-01

### Reproduction Signal
- Long-running subagent commands produce empty or truncated outputs in logs; debugging runs lose context.

### Root Cause
- Long command output or unbounded logs cause subagents or terminals to truncate results.

### Fix
- Split long-running tasks, keep terminal output small, and capture artifacts to files when needed.

### Prevention
- Add guidance in contributor notes: split large tasks and persist large outputs to files rather than relying on full terminal capture.

### memory_meta
- timestamp: 2026-10-01
- author: GitHub Copilot
