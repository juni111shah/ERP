---
id: TASK-006
title: POS void reverses cash, stock at original cost, and respects closed sessions
priority: P0
status: in-progress
schema: no
depends_on: [TASK-005]
area: pos
audit_ref: AUDIT-DEEP B4
commit:
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
- [ ] Void in a closed session returns 409 and changes nothing.
- [ ] After a cash void, expected cash equals opening cash plus remaining cash sales.
- [ ] Inventory value after sale+void equals the value before the sale.
- [ ] Two concurrent orders in one session produce correct totals.

## Tests required
Integration tests for the four criteria (the concurrency test may use parallel tasks).

## Do not touch
Order creation validation (TASK-005).

## What I did (Antigravity)

## Review (Claude)
