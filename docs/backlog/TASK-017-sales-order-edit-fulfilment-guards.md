---
id: TASK-017
title: Sales order edit, fulfilment and invoice due-date guards
priority: P0
status: open
schema: no
depends_on: []
area: sales
audit_ref: order-to-cash review 2026-10-30
commit:
---

## Problem
All in `backend/modules/ERP.Modules.Sales/Features/OrderFeatures.cs`:
1. `UpdateOrder` (~lines 195-215) only blocks `Invoiced`. Cancelled, Delivered and Processing orders can be rewritten, and `PopulateLinesAndTotals` does `order.Lines.Clear()`, so `DeliveredQuantity`, `InvoicedQuantity` and line ids are lost (delivered quantities can then be issued from stock again).
2. `UpdateOrderFulfillment` (~lines 245-285) has no status check: Draft and Cancelled orders can ship stock. A lowered `DeliveredQuantity` (delta < 0) changes the line without returning stock.
3. `ConvertOrderToInvoice` passes `DueDate: order.ExpectedDeliveryDate` (~line 322). A delivery date is not a payment due date; a past or empty value gives a wrong or rejected due date instead of the customer payment terms (`party.PaymentTermsDays`, `Invoicing.cs:79`).

## Impact
Double stock issue, lost delivery history, stock out of sync with delivered quantities, invoices due on the delivery date instead of the agreed terms.

## Change wanted
- `UpdateOrder`: allow only when status is `Draft`, or `Confirmed` with no delivered quantity; otherwise `ConflictException`.
- `UpdateOrderFulfillment`: require status `Confirmed` or `Processing`; reject a `DeliveredQuantity` below the current value with `FieldValidationException` (returns are out of scope).
- `ConvertOrderToInvoice`: pass `DueDate: null` so customer payment terms apply.

## Acceptance criteria
- [ ] Editing a Delivered, Cancelled or Invoiced order returns 409 and changes nothing.
- [ ] Fulfilment on a Draft or Cancelled order returns 409 and issues no stock.
- [ ] Lowering a delivered quantity returns 400.
- [ ] Invoice due date = invoice date + customer PaymentTermsDays.

## Tests required
Integration tests for each rule above, plus one asserting stock movements are not duplicated after a rejected call.

## Do not touch
Stock posting service internals, the order status transition table.

## What I did (Antigravity)

## Review (Claude)
