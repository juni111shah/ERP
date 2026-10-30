---
id: TASK-134
title: Converting a purchase order to a bill keeps the tax and uses a valid due date
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
`ConvertPurchaseOrderToBill` (backend/modules/ERP.Modules.Purchasing/Features/OrderFeatures.cs:260-355) builds `BillLineInput` without `TaxCodeId`, so the order's `TaxRate` is dropped and the bill total is below the order total. `DueDate: order.ExpectedDeliveryDate` (line ~343) goes straight to `CreateBill`; when it is earlier than today, `BillHandlers.ApplyAsync` (Finance/BillFeatures.cs:236) throws "The due date cannot be before the bill date" and the order can never be billed.

## Impact
Purchase bills understate tax and payables; orders with a past delivery date cannot be converted.

## Change wanted
- For each line with `TaxRate > 0` set `TaxCodeId` to the active tax code with the same `Rate` (prefer recoverable/default). If none exists, 409 "No active tax code with rate X% exists." Rate 0 gets no tax code.
- Pass `DueDate: null` (vendor terms apply) unless `ExpectedDeliveryDate` is on or after the bill date.

## Acceptance criteria
- [x] A PO with 5% tax converts to a bill whose `Tax` and `Total` equal the PO's `TaxTotal` and `TotalAmount` to the cent.
- [x] A PO with a past expected delivery date converts; due date is not before bill date.
- [x] A rate with no tax code returns 409 and creates no bill.

## Tests required
Integration tests for the three cases.

## Do not touch
Bill posting rules and the GR/IR line shape.

## What I did (Claude, branch claude/p2p)
- Bill lines from a purchase order carry the active tax code with the line rate (recoverable, then default, preferred). A rate with no code is 409 and creates no bill. The due date is null (vendor terms) unless the expected delivery date is on or after the bill date.
- Limit: tax is rounded per bill line while the order rounds per document, so an order whose lines split into received and unreceived parts can differ by a cent.
- Tests: 5% order converts with Tax and Total equal to the order; past delivery date converts; unknown rate is 409.

## Review (Claude)
