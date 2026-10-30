---
id: TASK-010
title: Make documents and their postings one transaction; add idempotency keys
priority: P0
status: done
schema: yes
depends_on: [TASK-004, TASK-006, TASK-008]
area: platform
audit_ref: AUD-016, AUD-017
commit: 1b0a284
owner: claude (worktree D:/ERP/work-claude, branch claude/impl; Antigravity skip)
---

## Problem
Handlers save the document, then call `IStockPostingService`/`IPostingService` which open their own transactions. A posting failure leaves a saved document without stock or journals. Create commands can be replayed (double click, retry) and post twice.

## Impact
Inconsistent books and duplicate postings.

## Change wanted
- Wrap each document handler (POS order, GRN, work-order complete, trip reconcile, order fulfilment, order to invoice) in `IUnitOfWork.InTransactionAsync` so document and postings commit together; make the stock posting service join the ambient transaction.
- Add an `Idempotency-Key` request header handled by an endpoint filter on the listed POST routes. Store (key, user, route, response hash/body, created at) in a new table `IdempotencyRecords` with a unique index and 24 h expiry cleanup in the nightly job. Replays return the stored response.
- Schema change: add the table via an EF migration plus the SQL script (do not apply it).

## Acceptance criteria
- [x] Forced posting exception leaves no document row.
- [x] Same `Idempotency-Key` sent twice returns the first response and creates one document and one set of postings.
- [x] Different keys create separate documents.

## Tests required
Integration tests using a failing posting stub; replay test.

## Do not touch
Existing migrations.

## What I did (Antigravity)

## Review (Claude)

Implemented by Claude in worktree D:/ERP/work-claude (branch claude/impl). Verified 2026-10-30: dotnet build 0 errors; unit + integration suite passes (238 unit, 230 integration passed, 3 SQL Server tests skipped without ERP_TEST_SQL and passed on LocalDB).
- Sales fulfilment, order-to-invoice and trip completion now run in one transaction (POS, GRN, work order and others already did). Added Idempotency-Key middleware, IdempotencyRecords table (EF migration 20261029235542 + database/scripts/0021, NOT applied), 24 h cleanup job, key reuse with a different body returns 422, failed responses release the key. Tests: IdempotencyTests (replay, different keys, reused key, failed request, ledger failure leaves no POS order).
