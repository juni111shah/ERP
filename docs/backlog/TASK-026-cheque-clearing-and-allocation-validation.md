---
id: TASK-026
title: Validate allocate-payment and issued-cheque clearing requests
priority: P2
status: open
schema: no
depends_on: []
area: finance
audit_ref: Claude analysis, Procure to pay
commit:
---

## Problem
`ClearIssuedCheque(InstrumentId, Date)` and `AllocatePayment` (`PaymentFeatures.cs`) have no FluentValidation validator. `ClearIssuedCheque` accepts any date, including `default` (0001-01-01) or a date before the cheque was issued, and posts the bank journal on it. `AllocatePayment` accepts an empty allocation list (a silent no-op) and allocates a payment to a bill dated after the payment.

## Impact
Bank journals dated 0001 or before issue, wrong bank reconciliation periods; confusing no-op calls.

## Change wanted
- `ClearIssuedChequeValidator`: `Date` not default; the handler rejects (`FieldValidationException("date", ...)`) a date earlier than the cheque `ReceivedOn` (issue date) or later than today.
- `AllocatePaymentValidator`: at least one allocation, each `Amount` greater than 0 and `MustBeMoney()`, no duplicate `BillId`. Keep the handler checks.
- In `AllocateAsync`, reject a bill whose `BillDate` is after the payment `Date` with `FieldValidationException("allocations", ...)`.

## Acceptance criteria
- [ ] Clearing with `0001-01-01`, with a date before the issue date, or a future date returns 400 on `date`.
- [ ] Allocate with an empty list returns 400.
- [ ] Allocating a payment dated 1 Feb to a bill dated 5 Feb returns 400.
- [ ] Existing cheque tests pass.

## Tests required
`PayablesTests`: one test per rule above.

## Do not touch
Cheques received flow, receipts.

## What I did (Antigravity)

## Review (Claude)
