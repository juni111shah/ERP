---
id: TASK-022
title: Run integration tests on SQL Server (or per-request SQLite connections) so concurrency can be tested
priority: P1
status: done
schema: no
depends_on: []
area: testing
audit_ref: found while implementing TASK-021
commit: d344a08
owner: claude (worktree D:/ERP/work-claude, branch claude/impl; Antigravity skip)
---

## Problem
`ApiFactory` hosts the API on SQLite with one shared connection and a global lock (`AppDbContext._sqliteTxLock`). Firing 20 parallel requests at `POST /api/v1/finance/receipts` fails with 500s such as "SqliteConnection does not support nested transactions", "no more rows available" and "SQL logic error" (reproduced while implementing TASK-021). Production uses SQL Server only.

## Impact
No integration test can exercise concurrent requests, row-version conflicts, number allocation races or SQL Server specific behaviour (decimal precision, collation, unique filtered indexes). Features such as TASK-021 and TASK-010 cannot be proven end to end, and SQLite-only passes can hide SQL Server failures.

## Change wanted
- Add a second test host mode used by a new test collection: SQL Server (LocalDB or a container, connection string from environment variable `ERP_TEST_SQL`; tests are skipped with a clear message when it is not set) with a fresh database per test class, created by applying the EF migrations.
- Alternative if SQL Server is not available in CI: give each request its own SQLite connection to a shared-cache in-memory or file database instead of one shared connection, and remove the global lock for that mode.
- Add `ParallelIssuingTests`: 20 parallel receipts on one legal entity all return 201 with 20 distinct numbers (this validates TASK-021); and 10 parallel invoice issues.
- Document in `docs/CONTRIBUTING.md` how to run the SQL Server collection.
- CI: run the SQL Server collection in a job with a SQL Server service container.

## Acceptance criteria
- [x] `ERP_TEST_SQL` unset: new collection is skipped, nothing else changes.
- [x] `ERP_TEST_SQL` set: `ParallelIssuingTests` pass.
- [x] CI runs the collection.

## Tests required
`ParallelIssuingTests`; a smoke test that the migrations apply to an empty SQL Server database.

## Do not touch
Production code, except where the test exposes a real defect (open a new card).

## What I did (Antigravity)

## Review (Claude)

Implemented by Claude in worktree D:/ERP/work-claude (branch claude/impl). Verified 2026-10-30: dotnet build 0 errors; unit + integration suite passes (238 unit, 230 integration passed, 3 SQL Server tests skipped without ERP_TEST_SQL and passed on LocalDB).
- SqlServerApiFactory (ERP_TEST_SQL, fresh database per factory, EF migrations applied), ParallelIssuingTests (migration smoke, 20 parallel receipts, 10 parallel invoice issues), CONTRIBUTING section, CI job Backend SQL Server tests. Run against LocalDB: 3 passed; skipped when ERP_TEST_SQL is unset. The run exposed a real defect in TASK-021: first-use races raise a duplicate-number error and three optimistic attempts cannot clear 20 contenders, so the retry behavior now also retries duplicate numbers and serializes numbered commands per workspace per process. TASK-021 criterion 1 is now verified.
