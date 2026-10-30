---
id: TASK-129
title: Validate dates and quantity/price precision on invoice and receipt inputs
priority: P2
status: open
schema: no
depends_on: []
area: sales
audit_ref: Claude analysis, Order to cash (second pass)
commit:
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
- [ ] Default/zero dates and dates far in the future return 400 with the key above.
- [ ] Quantity 1.00001 and price 9.12345 are rejected; 1.2345 is accepted.
- [ ] Existing seeded data and tests still pass.

## Tests required
Validator unit tests for each rule and one integration test per endpoint (invoice, receipt).

## Do not touch
Rounding of `Net`/`Tax` in `InvoicingService`.

## What I did (Antigravity)

## Review (Claude)
