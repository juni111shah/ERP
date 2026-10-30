---
id: TASK-130
title: Cumulative credited quantity per invoice line cannot exceed the invoiced quantity
priority: P2
status: ready-for-review
schema: yes
depends_on: []
area: sales
audit_ref: Claude analysis, Order to cash (second pass)
commit: 8e6ed74
owner: claude (worktree work-claude; Antigravity skip)
---

## Problem
`CreateCreditNoteFromInvoice` (backend/src/ERP.Application/Finance/InvoiceFeatures.cs:205-212) checks each requested quantity only against the original line quantity, and `EnsureWithinUncreditedAsync` (~line 222) caps only the document total. Two credit notes can each credit all of a cheap line while skipping an expensive one, as long as the sum stays under the invoice total.

## Impact
Revenue and tax are credited against the wrong lines (wrong income account and tax code), and returned-quantity reporting by line is wrong.

## Change wanted
- Add a nullable `SourceLineId` on `InvoiceLine` (EF migration file, snapshot and SQL script under `database/scripts`; do not apply it). Set it when a credit note line is built from an invoice line.
- Before creating the credit note, sum the quantities already credited per source line by the invoice's non-voided credit notes. Reject when `credited + requested > original quantity`, naming the line and what is left.
- With no lines given, propose only the uncredited remainder of each line, and fail with a clear message if nothing is left.

## Acceptance criteria
- [x] Crediting line 1 in full twice is rejected the second time.
- [x] Crediting 2 of 5 then 3 of 5 works; 4 of 5 after that is rejected.
- [x] Voiding a credit note frees its quantities again.
- [x] Existing credit-note tests still pass.

## Tests required
Integration tests for the three scenarios above, including void then re-credit.

## Do not touch
Existing migrations; the document-total cap (keep it).

## What I did (Claude, implemented in separate worktree)
- `InvoiceLine.SourceLineId` (nullable), carried by `InvoiceLineDraft`; set when a credit note line is built from an invoice line. EF migration `InvoiceLineSourceLine` and `database/scripts/0066_InvoiceLineSourceLine.sql` (one `ALTER TABLE ... ADD ... NULL`). NOT applied to any database; the migration chain was applied to an empty LocalDB database by the SQL Server test and passed, and `has-pending-model-changes` reports none.
- `CreateCreditNoteFromInvoice`: sums quantity already credited per source line over the invoice's non-voided credit notes and rejects `credited + requested > original` naming the line and what is left. With no lines given it proposes only the remainder, and a clear 409 when nothing is left. Voided credit notes do not count, so voiding frees quantity. The document-total cap stays.
- Editing a draft credit note keeps each line's `SourceLineId` (matched by position and description) and re-checks the cap, so an edit cannot get around it.
- Limits: credit notes raised before this change have no `SourceLineId`, so they are covered only by the total cap. In an edit, a line whose description was changed loses its link and falls back to the total cap.
- Tests: `CreditNoteLineCapTests` (full twice rejected; 2 then 3 ok, 4 rejected; void then re-credit; remainder proposal; everything credited; edit cannot bypass).
- Full suite: 286 unit + 364 integration passed, 8 skipped (SQL Server only; the relevant ones pass with ERP_TEST_SQL set).

## Review (Claude)
