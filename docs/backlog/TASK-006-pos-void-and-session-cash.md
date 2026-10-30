---
id: TASK-006
title: POS void reverses cash, stock at original cost, and respects closed sessions
priority: P0
status: done
schema: no
depends_on: [TASK-005]
area: pos
audit_ref: AUDIT-DEEP B4
commit: f521af6
---

## Problem
`OrderFeatures.cs` void returns stock at the sale price, does not reduce the session's `ExpectedCash`, and still edits totals of a closed session. Session totals are updated with read-modify-write, so two cashiers can overwrite each other.

## Impact
Inventory valued at selling price, drawer expectation overstated, closed shifts altered, lost updates.

## Change wanted
- Void only orders in an **open** session; otherwise `ConflictException` (a later return/credit flow is out of scope).
- Reduce `ExpectedCash` by the cash portion actually received (amount - change).
- Reverse the original COGS/inventory movement using the original issue cost (look up the order's stock movements by reference); do not use sale price.
- Update session totals with an atomic SQL increment (`ExecuteUpdate`) or a concurrency token retry.
- One transaction for status, session totals, stock reversal.

## Acceptance criteria
- [x] Void in a closed session returns 409 and changes nothing.
- [x] After a cash void, expected cash equals opening cash plus remaining cash sales.
- [x] Inventory value after sale+void equals the value before the sale.
- [x] Two concurrent orders in one session produce correct totals.

## Tests required
Integration tests for the four criteria (the concurrency test may use parallel tasks).

## Do not touch
Order creation validation (TASK-005).

## What I did (Antigravity)
- Added `ReverseIssueAsync` to `IStockPostingService` and implemented in `StockPostingService`: finds original stock movements for the order's reference where `Kind == MovementKind.Issue`, issuing offsetting receipt stock movements at original unit cost with the COGS account.
- In `VoidPosOrderCommand` (`OrderFeatures.cs`): verified that the session is in `Open` status; otherwise throws `ConflictException` (409) and aborts.
- Reduced `ExpectedCash` upon void by the actual cash received (`p.TenderedAmount > 0 ? (p.TenderedAmount - p.ChangeAmount) : p.Amount`).
- Converted session totals updates on order creation and void to atomic SQL increments (`ExecuteUpdateAsync`) scoped to `s.Status == PosSessionStatus.Open`.
- Enclosed order creation and void in single database transactions via `_unitOfWork.InTransactionAsync`.
- Added `_sqliteTxLock` semaphore guard in `AppDbContext.InTransactionAsync` to serialize transactions on SQLite test runner connections.
- Exposed `TotalReturns` on `PosSessionSummaryDto` in backend and frontend.
- Added four integration tests in `PosOrderTests.cs` verifying all four acceptance criteria. All unit and integration tests passing (223 unit, 205 integration).

## Review (Claude)

Verified 2026-10-30 in a clean `git archive HEAD` copy: `dotnet build` 0 errors; `dotnet test` 237 unit + 219 integration passed, 0 failed; `ng build` ok.
- Diff f521af6 matches the card. Void now needs an open session (409 otherwise, checked before any change), cash reduced by tendered minus change, stock reversed through `ReverseIssueAsync` at the original movement unit cost, session totals updated with `ExecuteUpdateAsync`, all inside `InTransactionAsync`. Four tests cover the four criteria.
- Not blocking, follow-ups: (1) the POS revenue journal is not reversed on void and journal failures are swallowed by `catch {}`; this is TASK-007/TASK-010. (2) The static `_sqliteTxLock` in `AppDbContext.InTransactionAsync` exists only to make the SQLite test host work; TASK-022 should remove the need for it. (3) The commit message lacks the `TASK-006:` prefix.
- Result: done.
