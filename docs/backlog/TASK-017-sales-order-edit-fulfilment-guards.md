---
id: TASK-017
title: Sales order edit, fulfilment and invoice due-date guards
priority: P0
status: ready-for-review
schema: no
depends_on: []
area: sales
audit_ref: order-to-cash review 2026-10-30
commit: 38e077e
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
- [x] Editing a Delivered, Cancelled or Invoiced order returns 409 and changes nothing.
- [x] Fulfilment on a Draft or Cancelled order returns 409 and issues no stock.
- [x] Lowering a delivered quantity returns 400.
- [x] Invoice due date = invoice date + customer PaymentTermsDays.

## Tests required
Integration tests for each rule above, plus one asserting stock movements are not duplicated after a rejected call.

## Do not touch
Stock posting service internals, the order status transition table.

## What I did (Antigravity)
- In `OrderFeatures.cs`:
  - In `UpdateOrder`: restricted edits strictly to `Draft` status, or `Confirmed` status where no quantities have been delivered yet (`order.Lines.All(l => l.DeliveredQuantity == 0)`). Any other status throws `ConflictException` (409).
  - In `UpdateOrderFulfillment`: required order status to be `Confirmed` or `Processing`, throwing `ConflictException` (409) otherwise. Added validation rejecting `DeliveredQuantity` less than current delivered quantity with `FieldValidationException` (400).
  - In `ConvertOrderToInvoice`: passed `DueDate: null` in `InvoiceInput` so that customer payment terms days (`party.PaymentTermsDays`) calculate the invoice due date.
- In `SalesOrderTests.cs`:
  - Added test `Editing_delivered_cancelled_or_invoiced_order_returns_conflict_and_changes_nothing` verifying 409 on delivered, cancelled, and invoiced edits.
  - Added test `Fulfillment_on_draft_or_cancelled_order_returns_conflict_and_issues_no_stock` verifying 409 and no inventory stock movements issued.
  - Added test `Lowering_delivered_quantity_returns_bad_request_and_issues_no_stock` verifying 400 and no extra stock movements issued.
  - Added test `Invoice_due_date_uses_customer_payment_terms_days` verifying due date equals invoice date + paymentTermsDays.

## Review (Claude)
