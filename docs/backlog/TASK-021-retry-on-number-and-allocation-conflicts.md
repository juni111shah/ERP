---
id: TASK-021
title: Retry document commands when a number or allocation hits a concurrency conflict
priority: P2
status: ready-for-review
schema: no
depends_on: []
area: platform
audit_ref: AUDIT-FULL finance core review
commit: 080f1f6
owner: claude (separate worktree; Antigravity skip this card)
---

## Problem
`NumberingService.NextAsync` increments the sequence in the caller's unit of work and relies on the row version to reject a concurrent allocation (`ERP.Application/Common/NumberingService.cs:39-41`). `SettlementService.AllocateAsync` also does read-modify-write on `Invoice.Paid`. Two users issuing invoices, receipts or POS orders at the same moment therefore get `DbUpdateConcurrencyException`, which the API turns into a 409 "The record was changed by someone else".

## Impact
Under normal parallel use (several cashiers, a billing run plus manual invoices) users see random failures on a simple "Issue" or "Save" and must retry by hand; for POS this blocks a sale at the counter.

## Change wanted
- Add a MediatR pipeline behavior `ConcurrencyRetryBehavior<TRequest, TResponse>` that applies only to requests implementing a marker interface `IRetryOnConcurrency`. On `DbUpdateConcurrencyException` it clears the change tracker state of the failed attempt (resolve a fresh scope or reload via the unit of work), waits 25-100 ms with jitter and re-runs the handler, at most 3 attempts, then lets the exception through.
- Mark as retryable the commands that issue numbered documents or allocate settlements: issue/create invoice, credit note, receipt, payment, bill, journal post, POS order, goods receipt.
- Do not retry commands that are not idempotent for the caller (anything that sends external messages).
- Log each retry at Information with the request name.

## Acceptance criteria
- [ ] 20 parallel `CreateReceipt` (or invoice issue) calls on one workspace all succeed with unique numbers and no 409.
- [ ] A conflict that persists after 3 attempts still returns 409.
- [ ] Requests without the marker are unaffected.

## Tests required
Integration test firing 20 concurrent requests; unit test for the behavior with a stub that throws twice then succeeds.

## Do not touch
Row version configuration; numbering format.

## What I did (Claude, implemented in separate worktree)
- Added `IRetryOnConcurrency` marker, `IResetOnRetry`, `ConcurrencyRetryBehavior` (3 attempts, 25-100 ms jitter, registered after validation), `IUnitOfWork.DiscardChanges()` (clears the change tracker; implemented in `AppDbContext` and the repository wrappers) and `NumberingService.ResetForRetry()` (drops sequences created but not saved, otherwise a retry would reuse a detached sequence).
- Marked as retryable: create invoice, issue invoice, apply credit note, create credit note from invoice, create receipt, create payment, create journal, post journal. These commit once (payments inside one transaction).
- Deliberately NOT marked: POS order and goods receipt (they save in several steps until TASK-010 makes them one transaction, so a retry could duplicate a committed document), void/update commands, PostBill (two saves).
- Tests: `ConcurrencyRetryBehaviorTests` (retries until success, gives up after 3 and rethrows, unmarked commands never retried). Full suite: 237 unit + 212 integration passed.
- NOT verified: acceptance criterion 1 (20 parallel receipts). The integration test host runs on one shared SQLite connection and fails with unrelated errors under parallel requests ("nested transactions", "no more rows"), so concurrency cannot be tested end to end here. See TASK-022.

## Review (Claude)
