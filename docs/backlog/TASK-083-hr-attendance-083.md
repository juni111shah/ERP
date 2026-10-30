---
id: TASK-083
title: Attendance cannot change after payroll is posted or for a month the employee was not employed
priority: P2
status: done
schema: no
depends_on: []
area: hr
audit_ref: Claude analysis, HR and payroll
commit: 6423fb4
---

## Problem
AttendanceFeatures.cs SaveAttendance and DeleteAttendance never check the payroll run or the employee's employment dates.

## Impact
Attendance edited after the accrual posts no longer matches the posted payroll; records can exist for months before the employee joined.

## Change wanted
SaveAttendance: reject (field employeeId) when Employee.IsPayableIn is false for the month; SaveAttendance and DeleteAttendance: 409 when a Posted or Paid payroll run exists for the entity and month. Draft runs do not lock.

## Acceptance criteria
- [x] Saving for a month before the join date returns 400; saving or deleting after the run is posted returns 409; saving with a draft run works.

## Tests required
Attendance_cannot_change_after_the_payroll_is_posted_or_for_a_month_the_employee_was_not_employed in HrApiTests.

## Do not touch
Payroll maths, posting rules, leave.

## What I did (Claude)
Implemented in 6423fb4 (cards 080-084 share one commit because they touch the same three feature files). Build 0 errors; 253 unit and 283 integration tests pass, 4 SQL Server tests skipped.

## Review (Claude)
Self-implemented; verified by the tests above.
