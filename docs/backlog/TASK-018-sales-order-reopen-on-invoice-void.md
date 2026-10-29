---
id: TASK-018
title: Reopen the sales order when its generated invoice is voided
priority: P1
status: in-progress
schema: no
depends_on: [TASK-016]
area: sales
audit_ref: order-to-cash review 2026-10-30
commit:
owner: claude (worktree D:/ERP/work-claude, branch claude/impl; Antigravity skip)
---

## Problem
`SalesOrder.InvoicedInvoiceId` (`backend/modules/ERP.Modules.Sales/Domain/SalesModels.cs:142`) is set by `ConvertOrderToInvoice`, but nothing reacts when that invoice is voided or deleted. `ConvertOrderToInvoice` then returns the stale (voided) invoice forever (`if (order.InvoicedInvoiceId.HasValue) return GetInvoice`) and the order stays `Invoiced`, a terminal state (`SalesTransitions.cs`). The only invoice-to-module notification today is `InvoicePaymentChanged` (`ERP.Application/Finance/SettlementService.cs:21`).

## Impact
A voided invoice cannot be re-issued from its order, the customer is never billed, and the order cannot be corrected.

## Change wanted
- Add an `InvoiceVoided(InvoiceId, SourceType, SourceId)` notification in `ERP.Application/Finance`, published by the `VoidInvoice` and `DeleteInvoice` handlers (only when `SourceType != null`).
- In the Sales module add an `INotificationHandler<InvoiceVoided>` for `SourceType == "salesorder"`: clear `InvoicedInvoiceId`, reset line `InvoicedQuantity` to 0 and set the status back to `Delivered` (all lines delivered) or `Processing`. Add the system-only transitions `Invoiced -> Delivered/Processing` to `SalesTransitions.Order`.
- `ConvertOrderToInvoice` must not return a voided invoice.

## Acceptance criteria
- [ ] Voiding the invoice of an order reopens the order and clears the link.
- [ ] Converting the order again creates a new invoice.
- [ ] Invoices without a SourceType are unaffected.

## Tests required
Integration test: convert, void, assert order status and link, convert again.

## Do not touch
Receipts, stock reversal.

## What I did (Antigravity)

## Review (Claude)
