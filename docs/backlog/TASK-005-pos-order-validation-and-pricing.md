---
id: TASK-005
title: POS orders priced and validated on the server
priority: P0
status: done
schema: no
depends_on: [TASK-003]
area: pos
audit_ref: AUDIT-DEEP A9, B5
commit: d624ada
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
- [x] Posting a lower unit price than the item's price returns 400 unless the override permission is held.
- [x] Posting `cashierUserId` of another user is ignored; stored cashier equals the caller.
- [x] Under-payment returns 400.
- [x] Discount above the configured maximum returns 400.

## Tests required
`PosOrderTests` integration tests for each criterion; unit test for totals rounding.

## Do not touch
Void flow (TASK-006) and ledger posting (TASK-007).

## What I did (Antigravity)
- Added `OrdersOverridePrice = "pos.orders.override-price"` permission and settings `pos.maxDiscountPercent`, `pos.allowPartialPayment` in `ERP.Modules.Pos`.
- Updated `CreatePosOrderCommand` and `PosOrderLineInput` to allow optional price/taxRate/cashier and ignore client-supplied cashier fields in favor of `ICurrentUser`.
- Added server-side item pricing, tax code lookup, discount cap, and price override permission check.
- Added payment validations (amount > 0, cash change math, non-cash tender matching, underpayment rejection unless `pos.allowPartialPayment` is set).
- Updated frontend POS terminal and API client to send only itemId, quantity, discountPercent, and payments.
- Added unit tests in `PosModuleTests.cs` for discount validation and half-away-from-zero totals rounding.
- Added integration tests in `PosOrderTests.cs` verifying price override rejection/acceptance, cashier identity override, underpayment rejection, and discount cap.

## Review (Claude)

- 2026-10-29: possible stall. status in-progress since 2026-10-25 (file untouched for >4 days), no TASK-005 commit and no code changes in the working tree.
- 2026-10-30: DONE. Verified commit d624ada against all 4 criteria; tests `PosOrderTests` (lower price 400 without override, cashier identity ignored, under-payment 400, discount cap 400, server pricing/tax) and unit tests in `PosModuleTests` pass. Clean `git archive HEAD` copy: `dotnet build` 0 errors, `dotnet test` 223 unit + 201 integration passed, `ng build` succeeded.
- Follow-ups (not blocking, not in criteria): lines without `ItemId` still use client price/tax; non-cash overpayment (sum > total) is not rejected. Candidates for a later card.
