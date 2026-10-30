---
id: TASK-082
title: Changing or deleting a payroll withdraws its pending approval
priority: P1
status: done
schema: no
depends_on: []
area: hr
audit_ref: Claude analysis, HR and payroll
commit: 6423fb4
---

## Problem
PayrollFeatures.cs: RecalculatePayroll, AdjustPayrollLine and DeletePayroll clear ApprovalRequestId without calling IApprovalService.WithdrawAsync (same pattern as TASK-027 for bills).

## Impact
The approver still sees and can approve a request for a payroll that was changed or deleted.

## Change wanted
Add WithdrawPendingAsync (withdraw when the request is Pending) and call it in the three handlers before the change.

## Acceptance criteria
- [x] Recalculating a pending payroll marks its request withdrawn; deleting a re-submitted payroll marks the new request withdrawn.

## Tests required
Changing_or_deleting_a_payroll_withdraws_its_pending_approval in HrApiTests.

## Do not touch
Payroll maths, posting rules, leave.

## What I did (Claude)
Implemented in 6423fb4 (cards 080-084 share one commit because they touch the same three feature files). Build 0 errors; 253 unit and 283 integration tests pass, 4 SQL Server tests skipped.

## Review (Claude)
Self-implemented; verified by the tests above.
