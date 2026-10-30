---
id: TASK-018
title: Reopen the sales order when its generated invoice is voided
priority: P1
status: done
schema: no
depends_on: [TASK-016]
area: sales
audit_ref: order-to-cash review 2026-10-30
commit: f692248
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
- [x] Voiding the invoice of an order reopens the order and clears the link.
- [x] Converting the order again creates a new invoice.
- [x] Invoices without a SourceType are unaffected.

## Tests required
Integration test: convert, void, assert order status and link, convert again.

## Do not touch
Receipts, stock reversal.

## What I did (Antigravity)

## Review (Claude)

Implemented by Claude in worktree D:/ERP/work-claude (branch claude/impl). Verified 2026-10-30: dotnet build 0 errors; unit + integration suite passes (238 unit, 230 integration passed, 3 SQL Server tests skipped without ERP_TEST_SQL and passed on LocalDB).
- Added system-only transitions Invoiced -> Confirmed/Processing/Delivered, `OrderInvoiceVoidedHandler` (reacts to the existing `InvoiceVoided` notification for SourceType salesorder), and blocked manual status change out of Invoiced. `DeleteInvoice` is not published because only drafts can be deleted and a draft is never linked to an order. Tests: reopen and re-convert; unrelated invoice void leaves orders alone.
