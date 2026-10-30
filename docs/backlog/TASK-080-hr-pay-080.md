---
id: TASK-080
title: Pay amounts are money and an adjustment cannot make pay negative
priority: P1
status: done
schema: no
depends_on: []
area: hr
audit_ref: Claude analysis, HR and payroll
commit: 6423fb4
---

## Problem
PayrollFeatures.cs AdjustPayrollLineValidator checks only Note; PayrollFeatures.cs Handle(AdjustPayrollLine) applies any Adjustment; EmployeeFeatures.cs EmployeeInputValidator checks salary and allowance amounts are >= 0 but not 2 decimals.

## Impact
A -5,000 adjustment on a 1,000 salary posts negative net pay; 1,000.123 salaries make payroll lines and the accrual journal disagree by rounding.

## Change wanted
AdjustPayrollLine: Adjustment MustBeMoney; reject (field adjustment) when the recalculated gross or net is below zero. EmployeeInput: MustBeMoney on basic, housing, transport, other allowances and recurring deductions.

## Acceptance criteria
- [x] Adjustment 10.005 returns 400; adjustment that makes net negative returns 400; employee salary 1000.123 returns 400; valid adjustment still works.

## Tests required
Pay_amounts_are_money_and_an_adjustment_cannot_make_pay_negative in HrApiTests.

## Do not touch
Payroll maths, posting rules, leave.

## What I did (Claude)
Implemented in 6423fb4 (cards 080-084 share one commit because they touch the same three feature files). Build 0 errors; 253 unit and 283 integration tests pass, 4 SQL Server tests skipped.

## Review (Claude)
Self-implemented; verified by the tests above.
