---
id: TASK-040
title: Backdated stock issues, reductions and transfers must not drive a later balance negative
priority: P1
status: done
schema: no
depends_on: []
area: inventory
audit_ref: Claude analysis, Inventory and Manufacturing
commit: 46f25c5
---

## Problem
`StockHandlers` (`backend/modules/ERP.Modules.Inventory/Features/StockFeatures.cs`) checks stock only up to the movement date (`PositionAsync(..., i.Date)`) for `IssueStock`, `AdjustStock` (negative) and `TransferStock`. It never looks at movements dated after it. Example: receive 10 on 1 Jan, issue 10 on 5 Jan, then issue 5 dated 2 Jan: position at 2 Jan is 10 so it passes, and the running balance on 5 Jan is -5. `StockCosting.Replay` then clamps the quantity at 0 and hides the shortfall.

## Impact
Stock history becomes impossible (negative balance in the past), the on-hand report silently shows 0 instead of the shortfall, and the cost booked to COGS no longer ties to the layers that existed on that date.

## Change wanted
- Add `StockCosting.FirstShortfall(IEnumerable<StockMovement> existing, StockMovement candidate)` returning the first date on which the running quantity (ordered by Date then CreatedAt, candidate last on its date) is below zero, or null.
- In `Issue`, `Adjust` (negative delta) and `Transfer` (out side) call it with all movements of that item/warehouse and throw `ConflictException("This would leave stock negative on <yyyy-MM-dd>; later movements already use that stock.")` before posting anything.
- Keep the existing 'Only N in stock' messages for the simple case.

## Acceptance criteria
- [ ] Receive 10 on 1 Jan, issue 10 on 5 Jan, then issue 5 dated 2 Jan returns 409 and nothing is posted.
- [ ] Same for a negative adjustment and a transfer out.
- [ ] A backdated issue that stays within stock on every later date still works.
- [ ] Existing inventory tests pass.

## Tests required
`InventoryTests`: the three 409 cases above plus the allowed backdated issue (ledger balance unchanged by the rejected calls).

## Do not touch
Costing formula, receipts, journals.

## What I did (Claude, on user request)
Added StockCosting.FirstShortfall and EnsureNoShortfallAsync in StockHandlers, called from issue, negative adjust and transfer out. Test: backdated issue/adjust/transfer rejected (409, nothing posted), allowed backdated issue still works.

## Review (Claude)
- Verified: dotnet build 0 errors; dotnet test 253 unit + 283 integration pass, 4 skipped (SQL Server).
