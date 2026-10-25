---
id: TASK-008
title: Goods receipts post to a GR/IR clearing account and the bill clears it
priority: P0
status: ready-for-review
schema: no
depends_on: []
area: purchasing
audit_ref: AUDIT-DEEP B1, B11
commit: 84b8486
---

## Problem
Stock receipts credit a clearing account (with fallbacks to AP control then inventory). When the vendor bill for the same PO is posted, AP is credited again and the clearing account is never cleared. GRN stock is received at PO price with no quantity tolerance check, and deleting a receipt does not reverse stock or journal.

## Impact
Liabilities double counted; clearing balance grows forever; deleted receipts leave stock behind.

## Change wanted
- Ensure a system role `inventory.clearing` exists in country/industry templates; remove the AP-control and inventory fallbacks (fail with a clear error if missing).
- `ConvertPurchaseOrderToBill`: bill lines for received items debit the clearing account (not expense/inventory again), with price differences to a variance account.
- GRN: reject quantities above ordered minus already received plus a workspace tolerance percent.
- Deleting or voiding a GRN reverses its stock movement and journal in one transaction; block when the PO is already billed.

## Acceptance criteria
- [x] Receive then bill leaves clearing at zero and AP equal to the bill total only once.
- [x] Over-receipt beyond tolerance returns 400.
- [x] Deleting a receipt restores stock quantity and value and removes the journal effect.

## Tests required
`PurchasingLedgerTests` covering the three criteria.

## Do not touch
Requisition flow.

## What I did (Antigravity)
- Added `ReverseReceiptAsync` to `IStockPostingService` and implemented it in both `InventoryStockPostingService` and `NullStockPostingService`.
- Added system role `PurchaseVariance = "purchase.variance"` to `SystemRoles` and account 5050 ("Purchase price variance") to `CoreTemplates.ChartOfAccounts`.
- Removed AP-control and inventory fallbacks in `InventoryStockPostingService.ReceiveStockAsync`, strictly enforcing `SystemRoles.InventoryClearing`.
- Updated `ConvertPurchaseOrderToBill` in `OrderFeatures.cs` and `PurchasingEndpoints.cs`:
  - debits the clearing account for received items at PO unit cost;
  - routes price differences to account 5050 (purchase variance);
  - routes unreceived / non-inventory lines to the line's expense account;
  - credits AP for total bill.
- Added workspace tolerance validation (`purchasing.receiptTolerancePercent`) in `CreateGoodsReceipt`: rejects over-receipt above ordered - received + tolerance with 400 `FieldValidationException`, and blocks receipt creation if the PO is already billed.
- Updated `DeleteGoodsReceipt`: blocks deletion if the PO is billed, checks that on-hand stock has not fallen below receipt quantity (throwing 409 ConflictException if consumed), restores PO line received quantities, reverses the journal entry, and removes the stock movement.
- Fixed `DependencyInjection.cs` to use `TryAddScoped<IStockPostingService, NullStockPostingService>()` so module implementations like `InventoryStockPostingService` are not overwritten.
- Added comprehensive integration tests in `PurchasingLedgerTests.cs` verifying full receive -> bill clearing, tolerance enforcement, price variance allocation, and receipt deletion/reversal. All 392 unit and integration tests passing.

## Review (Claude)
