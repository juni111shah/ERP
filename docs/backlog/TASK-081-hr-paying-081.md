---
id: TASK-081
title: Paying payroll validates the date and payroll commands retry on conflicts
priority: P1
status: done
schema: no
depends_on: []
area: hr
audit_ref: Claude analysis, HR and payroll
commit: 6423fb4
---

## Problem
PayrollFeatures.cs: PayPayroll has no validator (Date may be 0001-01-01 or before the payroll month) and PostPayroll/PayPayroll lack IRetryOnConcurrency and a transaction although they allocate journal numbers.

## Impact
Bank journals dated 0001 or before the pay month; two simultaneous payroll posts or payments return a 409 on a number clash.

## Change wanted
Add IRetryOnConcurrency to PostPayroll and PayPayroll, run both in uow.InTransactionAsync, add PayPayrollValidator (bank account not empty, date not default) and reject a date before the first day of the payroll month (field date). Paying before month end stays allowed.

## Acceptance criteria
- [x] Date 0001-01-01 and a date before the month return 400; a normal date pays; existing payroll tests pass.

## Tests required
Paying_a_payroll_needs_a_real_date_not_before_its_month in HrApiTests.

## Do not touch
Payroll maths, posting rules, leave.

## What I did (Claude)
Implemented in 6423fb4 (cards 080-084 share one commit because they touch the same three feature files). Build 0 errors; 253 unit and 283 integration tests pass, 4 SQL Server tests skipped.

## Review (Claude)
Self-implemented; verified by the tests above.
