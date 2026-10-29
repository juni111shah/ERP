---
id: TASK-020
title: Reject or round money amounts to two decimals in receipts, payments, cheques, bank and journals
priority: P1
status: in-progress
schema: no
depends_on: []
area: finance
audit_ref: AUDIT-FULL finance core review
commit:
owner: claude (separate worktree; Antigravity skip this card)
---

## Problem
`Money.Round` (2 decimals, away from zero) is used only in `Invoicing.cs` and `BillFeatures.cs`. Receipt, payment, cheque, bank transfer, manual journal and settlement allocation amounts are accepted with any number of decimals (`ReceiptFeatures.cs:50`, `PaymentFeatures.cs:49` only require `> 0`). Money columns have no explicit precision, so SQL Server stores them with its default `decimal(18,2)`.

## Impact
An amount such as `10.005` is held with three decimals while the journal balance check runs, then stored rounded by the database. Allocations (`SettlementService.AllocateAsync`) add unrounded amounts to `Invoice.Paid`, so an invoice can show a residue of a fraction of a cent, stay "PartlyPaid" or be marked Paid early, and the receipt, journal and invoice can disagree by a cent.

## Change wanted
- Add a FluentValidation extension `MustBeMoney()` (greater than zero, at most 2 decimal places) and apply it to every money input in receipts, payments, cheques, bank transfers/statement lines, settlement allocations, manual journal lines and credit note applications.
- In `SettlementService` and journal posting, apply `Money.Round` to amounts that come from computed values (for example auto-allocation), so stored values never have more than 2 decimals.
- Set explicit `HasPrecision(18, 2)` for the money columns of Settlement, SettlementAllocation, JournalLine, Invoice and Bill totals in the model configuration **only if** it matches the existing column type, so no migration is generated (verify with `dotnet ef migrations has-pending-model-changes`).

## Acceptance criteria
- [ ] A receipt, payment or allocation with 3 decimal places returns 400 on the amount field.
- [ ] Auto-allocation of an amount spread over several invoices never leaves a sub-cent remainder.
- [ ] The EF model still has no pending changes.
- [ ] Existing finance tests still pass.

## Tests required
Unit tests for `MustBeMoney`; integration tests for the receipt/payment rejection and an auto-allocation case with an awkward amount (for example 100.00 over three invoices of 33.33).

## Do not touch
Tax calculation rules; report logic.

## What I did (Antigravity)

## Review (Claude)
