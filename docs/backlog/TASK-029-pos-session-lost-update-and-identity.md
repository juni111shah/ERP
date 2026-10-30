---
id: TASK-029
title: Session drawer and close use atomic updates and the caller's identity
priority: P0
status: open
schema: no
depends_on: []
area: pos
audit_ref: POS analysis 2026-10-30
commit:
---

## Problem
1. `RecordDrawerTransactionCommand` loads the session tracked and does `session.ExpectedCash += amount` or `-=` (`SessionFeatures.cs:240-252`), then `SaveChanges`. EF writes the absolute value, so a sale committed in between (which updates `ExpectedCash` with `ExecuteUpdateAsync`, `OrderFeatures.cs:428-431`) is overwritten. `CloseSessionCommand` computes `CashDifference = ActualCash - ExpectedCash` from the same stale read (`SessionFeatures.cs:201`) and an order can land after the read but before the save.
2. Identity comes from the request body: `OpenedByUserId`/`CashierName` (`154-155`), `ClosedByUserId` (`198`), `PerformedByUserId` (`234`). Any caller can attribute drawer movements or closes to someone else (TASK-005 fixed this for orders only).

## Impact
Drawer shortages or surpluses are mis-stated, and the record of who opened, paid out or closed a shift can be forged.

## Change wanted
- Drawer transaction: wrap in `InTransactionAsync`; update `TotalPayIn`/`TotalPayOut`/`ExpectedCash` with `ExecuteUpdateAsync` increments filtered on `Status == Open` (0 rows -> `ConflictException`).
- Close: inside a transaction, close with `ExecuteUpdateAsync` filtered on `Status == Open`, then set `CashDifference` from the committed `ExpectedCash` (or compute it in the same SQL update). Late orders then fail with the existing "closed session" conflict.
- Take user id and display name from `ICurrentUser` in all three commands; remove or ignore `CashierUserId`, `CashierName`, `ClosedByUserId`, `PerformedByUserId` in the request bodies and update the frontend callers.

## Acceptance criteria
- [ ] A drawer PayIn recorded after a cash sale leaves `ExpectedCash = opening + sale + payin`, also when the two run interleaved on separate scopes.
- [ ] Closing a session computes `CashDifference` from `ExpectedCash` including all committed sales.
- [ ] A posted `PerformedByUserId`/`ClosedByUserId` of another user is ignored; stored values equal the caller.
- [ ] An order placed after close returns 409.

## Tests required
Integration tests in a new `PosSessionTests`, using the concurrency-capable host from TASK-022 for the interleaving case.

## Do not touch
Order pricing and payment rules, ledger posting.

## What I did (Antigravity)

## Review (Claude)
