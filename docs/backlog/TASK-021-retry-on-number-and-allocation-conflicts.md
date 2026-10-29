---
id: TASK-021
title: Retry document commands when a number or allocation hits a concurrency conflict
priority: P2
status: open
schema: no
depends_on: []
area: platform
audit_ref: AUDIT-FULL finance core review
commit:
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

## What I did (Antigravity)

## Review (Claude)
