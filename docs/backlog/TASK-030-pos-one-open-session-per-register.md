---
id: TASK-030
title: Enforce one open session per register in the database
priority: P1
status: open
schema: yes
depends_on: []
area: pos
audit_ref: POS analysis 2026-10-30
commit:
---

## Problem
`OpenSessionCommand` checks for an open session with a read (`SessionFeatures.cs:140-147`) and then inserts. The only index is non-unique (`PosModule.cs:110`, `HasIndex(OrgId, RegisterId, Status)`). Two simultaneous opens both pass the check. The session and its opening float are saved with two separate `SaveChanges` calls (`182-183`).

## Impact
Two open sessions on one register split sales and cash expectation, and `GetCurrentSessionQuery` (`269-271`) returns an arbitrary one.

## Change wanted
- Add a unique filtered index on the sessions table (`OrgId`, `RegisterId`) where `Status` is Open and `IsDeleted = 0` in `PosModule.cs`, an EF migration file, its SQL script under `database/scripts`, and the snapshot update. Do not apply it.
- In `OpenSessionCommand` translate the unique violation into the existing `ConflictException` message (reuse the helper used by numbering or idempotency retries if one exists).
- Use `InTransactionAsync` so the session and the opening float commit together.

## Acceptance criteria
- [ ] 10 parallel opens on one register give exactly 1 success and 9 conflicts.
- [ ] Opening again after close succeeds.
- [ ] A failure inserting the float leaves no orphan session.

## Tests required
Integration test with the concurrency-capable host (TASK-022); migration and SQL script present.

## Do not touch
Existing migrations.

## What I did (Antigravity)

## Review (Claude)
