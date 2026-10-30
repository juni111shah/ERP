---
id: TASK-027
title: Editing or deleting a bill withdraws its pending approval request
priority: P1
status: done
schema: no
depends_on: []
area: finance
audit_ref: Claude analysis, Procure to pay
commit: f5d1bc9
---

## Problem
`UpdateBill` (`BillFeatures.cs`) resets a PendingApproval bill to Draft and sets `ApprovalRequestId = null`, but the `ApprovalRequest` stays `Pending`. `DeleteBill` removes a PendingApproval bill and also leaves the request. `IApprovalService.WithdrawAsync` exists (`Abstractions.cs:65`) but is not called here. The approver can still see and approve a request for a document or amount that no longer exists.

## Impact
Stale approvals in the approver inbox, and an audit trail showing approvals with no matching bill.

## Change wanted
- In `UpdateBill`, when the bill has an `ApprovalRequestId` whose request is `Pending`, call `approvals.WithdrawAsync(id)` before clearing the id. If the request is already decided, only clear the link.
- In `DeleteBill`, withdraw a Pending request before removing the bill.
- Check whether `InvoiceFeatures` has the identical pattern; record it as a finding in this card's Review. Do not change invoices here.

## Acceptance criteria
- [ ] Editing a bill that is waiting for approval sets its request to Withdrawn and it no longer shows as pending in the approvals inbox.
- [ ] Deleting a pending bill sets the request to Withdrawn.
- [ ] Posting the edited bill submits a fresh request (existing behaviour kept).

## Tests required
Extend `A_bill_over_the_policy_amount_waits_for_another_user_to_approve_it` or add a sibling test in `PayablesTests` for edit and delete.

## Do not touch
Approval policies and the decide flow.

## What I did (Claude, on user request)
UpdateBill and DeleteBill withdraw a Pending approval request before clearing/removing the link. Finding: InvoiceFeatures has no approval flow, so no equivalent pattern exists there. Test extends PayablesTests with edit, re-post and delete.

## Review (Claude)
- Verified: dotnet build 0 errors; dotnet test 238 unit + 241 integration pass, 4 skipped (SQL Server). 
