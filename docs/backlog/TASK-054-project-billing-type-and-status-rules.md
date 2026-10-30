---
id: TASK-054
title: Billing honours the project billing type and status
priority: P1
status: done
schema: no
depends_on: []
area: projects
audit_ref: Claude analysis, Projects and Services
commit: HASH
---

## Problem
BillingFeatures.cs: `BillProject` ignores `Project.BillingType` and `Status`, so a NonBillable project can be invoiced, a FixedFee project also bills hourly timesheets on top of its milestones, and Planning/Cancelled projects can be invoiced; `GetBillingOverview` lists NonBillable projects.

## Impact
Customers are invoiced for work that was agreed as non-billable or as a fixed fee; cancelled projects generate invoices.

## Change wanted
In `BillProject`: NonBillable -> 409; Planning or Cancelled -> 409; FixedFee bills milestones only (timesheets stay unbilled). In `GetBillingOverview` skip NonBillable projects and ignore timesheets for FixedFee.

## Acceptance criteria
- [x] Billing a NonBillable or Planning project returns 409.
- [x] A FixedFee project with a timesheet and an achieved milestone bills 0 timesheets and 1 milestone; the timesheet stays unbilled.

## Tests required
ProjectBillingRulesTests (2 tests).

## Do not touch
Invoice creation, existing billing tests.

## What I did (Claude, parallel agent)
Implemented as described; no schema change. dotnet build 0 errors; dotnet test backend/ERP.sln passes (253 unit, 289 integration, 4 skipped SQL Server).

## Review (Claude)
Self-implemented on branch claude/proj; build and tests green.
