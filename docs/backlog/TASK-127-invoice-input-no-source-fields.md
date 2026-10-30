---
id: TASK-127
title: The public invoice API must not accept SourceType and SourceId
priority: P2
status: in-progress
schema: no
depends_on: []
area: sales
audit_ref: Claude analysis, Order to cash (second pass)
commit:
owner: claude (worktree work-claude; Antigravity skip)
---

## Problem
`InvoiceInput` (backend/src/ERP.Application/Finance/InvoiceFeatures.cs:12) exposes `SourceType` and `SourceId`, and `ToDraft` (line 238) copies them. `POST /api/v1/invoices` therefore lets any user with `invoices.write` forge a link to a sales order, deal or milestone, or an `invoice` source that drives `EnsureWithinUncreditedAsync` (line ~222) with an arbitrary SourceId.

## Impact
Manual invoices can masquerade as module-generated ones (wrong SourceType in lists and filters), and a forged `invoice` link makes the credit cap compare against an unrelated document.

## Change wanted
- Remove `SourceType`/`SourceId` from the screen-facing `InvoiceInput`, or ignore them in `ToDraft`. Only module code and `CreateCreditNoteFromInvoice` set them through `InvoiceDraft`.
- `UpdateInvoice` already preserves the stored link; keep that.
- Update the frontend invoice form model if it sends these fields.

## Acceptance criteria
- [ ] A POST/PUT with sourceType/sourceId in the body creates or leaves an invoice with SourceType and SourceId null (or unchanged on update).
- [ ] Credit notes from `/credit-note` and module-generated invoices keep their links.
- [ ] `dotnet test` and `ng build` pass.

## Tests required
Integration test: post an invoice with a forged sourceType "salesorder" and a random sourceId; assert both come back null. Existing credit-note linkage tests still pass.

## Do not touch
Module billing runs; the `InvoiceDraft` record.

## What I did (Antigravity)

## Review (Claude)
