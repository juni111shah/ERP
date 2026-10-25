---
id: TASK-005
title: POS orders priced and validated on the server
priority: P0
status: open
schema: no
depends_on: [TASK-003]
area: pos
audit_ref: AUDIT-DEEP A9, B5
commit:
---

## Problem
`CreatePosOrderCommand` trusts the client's `unitPrice`, `taxRate`, `discountPercent`, `cashierUserId` and `cashierName`. Payments are not compared with the total (an under-paid order is stored as `Paid`). Discount above 100 is not validated.

## Impact
Anyone with `pos.orders.create` can sell at any price and tax, attribute sales to any user, and close orders without full payment.

## Change wanted
- Take cashier id and name from `ICurrentUser`; drop them from the command (keep the UI sending nothing).
- For each line with an `ItemId`, load the item and use its sales price and tax code; accept a client price only when the user has a new permission `pos.orders.override-price`, and cap discount by a workspace setting `pos.maxDiscountPercent`.
- Validate: sum of payment amounts >= order total; cash change = tendered - amount; non-cash tender equals amount; each amount > 0; supported method.
- Set `PaymentStatus` from the comparison (`Paid`, `Partial`); reject partial unless a setting allows it.
- Update the terminal page to send only item id, quantity, discount, payments, and to show server totals returned.

## Acceptance criteria
- [ ] Posting a lower unit price than the item's price returns 400 unless the override permission is held.
- [ ] Posting `cashierUserId` of another user is ignored; stored cashier equals the caller.
- [ ] Under-payment returns 400.
- [ ] Discount above the configured maximum returns 400.

## Tests required
`PosOrderTests` integration tests for each criterion; unit test for totals rounding.

## Do not touch
Void flow (TASK-006) and ledger posting (TASK-007).

## What I did (Antigravity)

## Review (Claude)
