---
id: TASK-130
title: Cumulative credited quantity per invoice line cannot exceed the invoiced quantity
priority: P2
status: open
schema: yes
depends_on: []
area: sales
audit_ref: Claude analysis, Order to cash (second pass)
commit:
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
- [ ] Crediting line 1 in full twice is rejected the second time.
- [ ] Crediting 2 of 5 then 3 of 5 works; 4 of 5 after that is rejected.
- [ ] Voiding a credit note frees its quantities again.
- [ ] Existing credit-note tests still pass.

## Tests required
Integration tests for the three scenarios above, including void then re-credit.

## Do not touch
Existing migrations; the document-total cap (keep it).

## What I did (Antigravity)

## Review (Claude)
