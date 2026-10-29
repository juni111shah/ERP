---
id: TASK-022
title: Run integration tests on SQL Server (or per-request SQLite connections) so concurrency can be tested
priority: P1
status: open
schema: no
depends_on: []
area: testing
audit_ref: found while implementing TASK-021
commit:
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
- [ ] `ERP_TEST_SQL` unset: new collection is skipped, nothing else changes.
- [ ] `ERP_TEST_SQL` set: `ParallelIssuingTests` pass.
- [ ] CI runs the collection.

## Tests required
`ParallelIssuingTests`; a smoke test that the migrations apply to an empty SQL Server database.

## Do not touch
Production code, except where the test exposes a real defect (open a new card).

## What I did (Antigravity)

## Review (Claude)
