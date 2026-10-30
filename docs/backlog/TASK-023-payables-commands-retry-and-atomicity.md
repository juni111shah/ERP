---
id: TASK-023
title: Make bill posting, payment allocation/void and cheque clearing retryable and atomic
priority: P1
status: done
schema: no
depends_on: []
area: finance
audit_ref: Claude analysis, Procure to pay
commit: f5d1bc9
---

## Problem
TASK-021 listed "bill" among the numbered commands to retry, but only `CreatePayment` (`PaymentFeatures.cs:30`) carries `IRetryOnConcurrency`. `PostBill` (`BillFeatures.cs`, `numbering.NextAsync("bill", ...)` then `posting.PostAsync` then `SaveChangesAsync`) allocates a number and has no marker and no unit of work. `AllocatePayment`, `VoidPayment`, `VoidBill` and `ClearIssuedCheque` do several writes (journal, bill `Paid`, settlement, cheque status) in separate steps without `uow.InTransactionAsync`, which `CreatePayment` uses.

## Impact
Two users posting bills at the same moment get a 409 on a simple "Post". A failure between the journal post and the status update (VoidPayment, ClearIssuedCheque) can leave a reversed or cleared journal with a bill/cheque still showing the old state.

## Change wanted
- Add `IRetryOnConcurrency` to `PostBill`, `AllocatePayment`, `VoidPayment`, `VoidBill`, `ClearIssuedCheque`.
- Wrap the handlers of `PostBill`, `VoidBill`, `VoidPayment`, `AllocatePayment` and `ClearIssuedCheque` in `uow.InTransactionAsync` like `CreatePayment`, so journal and document state commit together. First check whether `IPostingService.PostAsync` / `ReverseTodayAsync` already save; if they do, make sure they join the transaction rather than commit alone.
- No other behaviour change.

## Acceptance criteria
- [ ] 10 parallel `PostBill` calls on different draft bills of one workspace all succeed with unique numbers.
- [ ] If the journal post fails inside `VoidPayment`, the payment, bills and cheque keep their old state (nothing half written).
- [ ] Existing payables tests pass.

## Tests required
In `PayablesTests` (SQL Server host from TASK-022): parallel post test; a failing-journal rollback test for `VoidPayment` (for example a locked books date).

## Do not touch
Posting rules, account selection, the approval flow.

## What I did (Claude, on user request)
Added IRetryOnConcurrency to PostBill, VoidBill, AllocatePayment, VoidPayment, ClearIssuedCheque; each handler now runs inside uow.InTransactionAsync (PostingService.PostAsync only adds to the context, so journal and document state commit together). Tests: rollback test on VoidPayment (books locked to today); 10-parallel PostBill test in ParallelIssuingTests (SQL Server, skipped here: ERP_TEST_SQL not set).

## Review (Claude)
- Verified: dotnet build 0 errors; dotnet test 238 unit + 241 integration pass, 4 skipped (SQL Server). The parallel criterion is unproven until run against SQL Server.
