---
id: TASK-010
title: Make documents and their postings one transaction; add idempotency keys
priority: P0
status: in-progress
schema: yes
depends_on: [TASK-004, TASK-006, TASK-008]
area: platform
audit_ref: AUD-016, AUD-017
commit:
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
- [ ] Forced posting exception leaves no document row.
- [ ] Same `Idempotency-Key` sent twice returns the first response and creates one document and one set of postings.
- [ ] Different keys create separate documents.

## Tests required
Integration tests using a failing posting stub; replay test.

## Do not touch
Existing migrations.

## What I did (Antigravity)

## Review (Claude)
