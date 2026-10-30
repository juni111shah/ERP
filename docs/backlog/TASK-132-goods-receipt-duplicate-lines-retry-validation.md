---
id: TASK-132
title: Goods receipt lines are checked together, commands retry on conflicts, dates and precision are validated
priority: P1
status: open
schema: no
depends_on: []
area: purchasing
audit_ref: Claude analysis, Procure to pay (second pass)
commit:
---

## Problem
1. The over-receipt check (backend/modules/ERP.Modules.Purchasing/Features/ReceiptFeatures.cs:138-155) tests each request line on its own. Two lines with the same `PurchaseOrderLineId`, each within the remaining quantity, both pass and `ReceivedQuantity` ends above tolerance (line 190).
2. `CreateGoodsReceipt` and `DeleteGoodsReceipt` (lines 71-72) are not `IRetryOnConcurrency`; two receipts racing on one order can read the same `ReceivedQuantity`.
3. `GoodsReceiptInputValidator` (lines 74-88) has no rule for `ReceiptDate` (0001-01-01 becomes the stock movement date) and no decimal limit on `QuantityReceived`.

## Impact
Over-receipt beyond tolerance, lost updates on `ReceivedQuantity`, stock movements dated year 1.

## Change wanted
- Sum `QuantityReceived` per `PurchaseOrderLineId` before comparing with remaining plus tolerance.
- Add `IRetryOnConcurrency` to both commands (handlers already use `InTransactionAsync`).
- `ReceiptDate` uses `MustBePlausibleDate()`, `QuantityReceived` uses `MustHaveAtMostDecimals(4)` (ERP.Application/Finance/MoneyValidation.cs).

## Acceptance criteria
- [ ] Two lines for the same order line whose sum exceeds remaining plus tolerance return 400 on `lines`.
- [ ] Both commands are `IRetryOnConcurrency`.
- [ ] Receipt date 0001-01-01 or more than a year ahead returns 400 on `receiptDate`; quantity 1.00001 returns 400.

## Tests required
Integration tests for the duplicate-line case and date/precision rules; a unit test that both commands are retryable.

## Do not touch
Tolerance setting semantics.

## What I did (Antigravity)

## Review (Claude)
