---
id: TASK-127
title: The public invoice API must not accept SourceType and SourceId
priority: P2
status: done
schema: no
depends_on: []
area: sales
audit_ref: Claude analysis, Order to cash (second pass)
commit: 710861a
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
- [x] A POST/PUT with sourceType/sourceId in the body creates or leaves an invoice with SourceType and SourceId null (or unchanged on update).
- [x] Credit notes from `/credit-note` and module-generated invoices keep their links.
- [x] `dotnet test` and `ng build` pass.

## Tests required
Integration test: post an invoice with a forged sourceType "salesorder" and a random sourceId; assert both come back null. Existing credit-note linkage tests still pass.

## Do not touch
Module billing runs; the `InvoiceDraft` record.

## What I did (Claude, implemented in separate worktree)
- Three module billing runs (Projects, Sales, Services) send `SourceType`/`SourceId` through `CreateInvoice`, so the fields stay on `InvoiceInput`, but both are now `[JsonIgnore]`: a request body can no longer set them, while code still can. `UpdateInvoice` already kept the stored link.
- Tests: forged `sourceType`/`sourceId` on POST and PUT come back null; a credit note from `/credit-note` keeps its link. Frontend never sent these fields, no change.

## Review (Claude)

`SourceType`/`SourceId` are `[JsonIgnore]` on `InvoiceInput`; module billing and `/credit-note` set them in code. `A_forged_source_link_in_the_request_body_is_ignored` and `A_credit_note_from_an_invoice_keeps_its_link` pass.
Clean `git archive` copy of HEAD (7479c1b): `dotnet build backend/ERP.sln` 0 errors; `dotnet test`: unit 286 passed, integration 364 passed, 8 skipped (SQL Server only), 0 failed. Reviewed 2026-10-30.
