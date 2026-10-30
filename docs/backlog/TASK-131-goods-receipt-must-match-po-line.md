---
id: TASK-131
title: Goods receipts are accepted only for open purchase orders and take item data from the order line
priority: P1
status: open
schema: no
depends_on: []
area: purchasing
audit_ref: Claude analysis, Procure to pay (second pass)
commit:
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
- [ ] Receipt against a Draft, Submitted, Cancelled or Billed order returns 409.
- [ ] Receipt with a legal entity different from the order's returns 400 on `legalEntityId`.
- [ ] A request line with another `ItemId` than the order line returns 400; stock is received only for the order line's item.
- [ ] A normal receipt against an approved order still works and updates the order status.

## Tests required
Integration tests for each case; assert no stock movement is created in the rejected cases.

## Do not touch
GR/IR posting, tolerance calculation, numbering.

## What I did (Antigravity)

## Review (Claude)
