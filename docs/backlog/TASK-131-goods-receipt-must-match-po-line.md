---
id: TASK-131
title: Goods receipts are accepted only for open purchase orders and take item data from the order line
priority: P1
status: ready-for-review
schema: no
depends_on: []
area: purchasing
audit_ref: Claude analysis, Procure to pay (second pass)
commit: e5cbf2c
owner: claude (branch claude/p2p; Antigravity skip)
---

## Problem
`CreateGoodsReceipt` (backend/modules/ERP.Modules.Purchasing/Features/ReceiptFeatures.cs:116-205) only blocks a `Billed` order (line 124), so a receipt can be posted against a Draft, Submitted or Cancelled order. It never compares `order.LegalEntityId` with `Input.LegalEntityId`, and the line `ItemId`, `ItemCode`, `Description` and `UnitOfMeasure` come from the request, not the order line (lines 177-203): stock is received for `l.ItemId` at the price of an unrelated PO line.

## Impact
Stock and the GR/IR clearing journal can be created for an order nobody approved, for another legal entity, or for a different item than was ordered.

## Change wanted
- Allow receipts only when the order status is `Approved` or `PartiallyReceived`; otherwise 409 "Only an approved purchase order can be received."
- 400 on `legalEntityId` when `order.LegalEntityId != Input.LegalEntityId`.
- Copy `ItemId`, `ItemCode`, `Description`, `UnitOfMeasure` from the matching `PurchaseOrderLine` when building `GoodsReceiptLine` and calling `ReceiveStockAsync`. A request `ItemId` different from the line's is a 400 on `lines`.

## Acceptance criteria
- [x] Receipt against a Draft, Submitted, Cancelled or Billed order returns 409.
- [x] Receipt with a legal entity different from the order's returns 400 on `legalEntityId`.
- [x] A request line with another `ItemId` than the order line returns 400; stock is received only for the order line's item.
- [x] A normal receipt against an approved order still works and updates the order status.

## Tests required
Integration tests for each case; assert no stock movement is created in the rejected cases.

## Do not touch
GR/IR posting, tolerance calculation, numbering.

## What I did (Claude, branch claude/p2p)
- Receipts only for `Approved`/`PartiallyReceived` orders (409 otherwise), same legal entity as the order (400 `legalEntityId`); a request `ItemId` that differs from the order line is 400; item, code, description and unit are copied from the order line and stock is received for the order line item.
- Existing `PurchasingLedgerTests` now approve the order before receiving; the second over-receipt after a full receipt is now 409 (status) instead of 400 (quantity).
- Tests: `GoodsReceiptRulesTests` (integration and unit), written by a parallel agent and reviewed by me. No stock movement is created in rejected cases.

## Review (Claude)
