---
id: TASK-129
title: Validate dates and quantity/price precision on invoice and receipt inputs
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
`InvoiceInputValidator` (backend/src/ERP.Application/Finance/InvoiceFeatures.cs:73-85) has no rule for `InvoiceDate` (a missing date binds to 0001-01-01), no `DueDate >= InvoiceDate` rule (only a generic error in Invoicing.cs:76) and no limit on decimal places for `Quantity` and `Price`. `ReceiptInputValidator` (ReceiptFeatures.cs:45-54) has no rule for `Date`.

## Impact
A receipt or invoice dated year 1 posts a journal in a non-existent period and breaks ageing and period reports; prices with many decimals cause rounding drift between line `Net` and the unit price.

## Change wanted
- Require `InvoiceDate`, `DueDate` (when given) and receipt `Date` to be after 2000-01-01 and at most 1 year in the future.
- Quantity and Price at most 4 decimal places (add a `MustHaveAtMostDecimals(int places)` helper beside `MustBeMoney` in MoneyValidation.cs if none exists).
- Error keys: `invoiceDate`, `dueDate`, `date`, and the line quantity/price fields.

## Acceptance criteria
- [x] Default/zero dates and dates far in the future return 400 with the key above.
- [x] Quantity 1.00001 and price 9.12345 are rejected; 1.2345 is accepted.
- [x] Existing seeded data and tests still pass.

## Tests required
Validator unit tests for each rule and one integration test per endpoint (invoice, receipt).

## Do not touch
Rounding of `Net`/`Tax` in `InvoicingService`.

## What I did (Claude, implemented in separate worktree)
- `MoneyValidation` gained `MustBePlausibleDate` (after 2000-01-01, at most one year ahead), `MustHaveAtMostDecimals(places)` and testable `IsPlausibleDate`/`HasAtMostDecimals`.
- Invoice: `invoiceDate` and `dueDate` plausible, due date not before invoice date (error key `dueDate`), line quantity and price at most 4 decimals. Receipt: `date` plausible. `InvoicingService` rounding left untouched.
- Tests: unit tests for the helpers and boundaries; integration tests for 0001-01-01, 1999-12-31 and 2099-01-01 on invoice and receipt, due before invoice date, quantity 1.00001 / price 9.12345 rejected and 1.2345 / 9.1234 accepted. Whole suite still passes, so seeded data and demo history are within range.

## Review (Claude)

Plausible-date rule (after 2000-01-01, at most one year ahead), due date not before invoice date, quantity/price at most 4 decimals; `InvoicingService` rounding untouched. Unit and integration tests cover the boundaries.
Clean `git archive` copy of HEAD (7479c1b): `dotnet build backend/ERP.sln` 0 errors; `dotnet test`: unit 286 passed, integration 364 passed, 8 skipped (SQL Server only), 0 failed. Reviewed 2026-10-30.
