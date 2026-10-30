---
id: TASK-138
title: Payment and cheque dates are validated; bill edit and delete retry on conflicts
priority: P2
status: ready-for-review
schema: no
depends_on: []
area: purchasing
audit_ref: Claude analysis, Procure to pay (second pass)
commit: e5cbf2c
owner: claude (branch claude/p2p; Antigravity skip)
---

## Problem
`PaymentInputValidator` (Finance/PaymentFeatures.cs:57-69) has no rule for `Date` or `ChequeDate`; `ClearIssuedChequeValidator` (lines 43-46) only rejects the default date, so 0002-01-01 passes. `UpdateBill` and `DeleteBill` (BillFeatures.cs:35-36) are not `IRetryOnConcurrency` while `PostBill` is.

## Impact
Payments and clearances posted to year 1 or the far future; intermittent errors when editing a bill that is being posted.

## Change wanted
- `Date`, `ChequeDate` (when given) and the clearing `Date` use `MustBePlausibleDate()`.
- Add `IRetryOnConcurrency` to `UpdateBill` and `DeleteBill`.

## Acceptance criteria
- [x] Payment date 0001-01-01 or 2099, cheque date 1999 and clearing date 0002-01-01 each return 400 with the matching key.
- [x] `UpdateBill` and `DeleteBill` are retryable.

## Tests required
Validator tests, one integration test per rule, a unit test for the retry markers.

## Do not touch
Posting behaviour of payments and cheques.

## What I did (Claude, branch claude/p2p)
- Payment `Date`, `ChequeDate` and the clearing `Date` use `MustBePlausibleDate`. `UpdateBill` and `DeleteBill` are `IRetryOnConcurrency`.
- Tests: validator boundary unit tests, integration tests for payment, cheque and clearing dates, retry markers.

## Review (Claude)
