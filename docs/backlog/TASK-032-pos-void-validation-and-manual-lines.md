---
id: TASK-032
title: Void needs a reason; free-text lines need price override permission
priority: P1
status: open
schema: no
depends_on: []
area: pos
audit_ref: TASK-005 review follow-up
commit:
---

## Problem
- `VoidPosOrderCommand(OrderId, Reason)` has no validator (`OrderFeatures.cs:62-64`), so an empty reason is stored as `Voided: ` and a null reason is accepted into the order notes.
- Lines without `ItemId` use the client's price and tax rate unchecked (`OrderFeatures.cs:301-305`), bypassing the server pricing and the `pos.orders.override-price` rule from TASK-005. They also never issue stock. `TaxRate` is not range-checked anywhere.

## Impact
Voids carry no audit reason. A cashier with only `pos.orders.create` can sell arbitrary free-text items at any price with a 0% (or negative) tax rate.

## Change wanted
- Add `VoidPosOrderValidator`: `OrderId` not empty, `Reason` not empty, max 500 characters.
- For lines without `ItemId`: require `OrdersOverridePrice` (reject with the same error shape used for the price override), require `UnitPrice` and `ItemCode`.
- Validate `TaxRate` between 0 and 100 for every line.
- Only `Completed` orders can be voided (explicit status check besides the existing `Voided` one).

## Acceptance criteria
- [ ] Void with a blank reason returns 400.
- [ ] A free-text line by a caller without the override permission is rejected.
- [ ] A free-text line with the override permission and valid price and tax succeeds.
- [ ] Tax rate above 100 or below 0 returns 400.

## Tests required
Integration tests in `PosOrderTests`, one per criterion.

## Do not touch
Void accounting (TASK-006 and TASK-007 behaviour).

## What I did (Antigravity)

## Review (Claude)
