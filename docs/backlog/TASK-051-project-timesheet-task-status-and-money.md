---
id: TASK-051
title: Timesheets validate task, project state and money precision
priority: P1
status: done
schema: no
depends_on: []
area: projects
audit_ref: Claude analysis, Projects and Services
commit: a9ededb
---

## Problem
TimesheetFeatures.cs (CreateTimesheet/UpdateTimesheet): `TaskId` is not checked against the project; time can be logged on Completed/Cancelled projects; `TotalAmount = Math.Round(hours*rate, 4)` keeps 4 decimals while invoice lines round to 2; billable flag is accepted on NonBillable projects.

## Impact
Hours booked against another project task, closed projects and non-billable projects showing billable time, and timesheet totals that differ from the invoice line amount.

## Change wanted
Check the task belongs to the project (400 on `taskId`); `ProjectRules.EnsureOpen` on create; round with `Money.Round` (2 decimals); force `IsBillable = false` on NonBillable projects, on create and update.

## Acceptance criteria
- [x] A timesheet with a task of another project returns 400 on create and update.
- [x] Hours 1.333 at 10.005 gives totalAmount 13.34.
- [x] Logging time on a cancelled project returns 409.
- [x] Time on a NonBillable project is saved with isBillable false.

## Tests required
ProjectTimesheetRulesTests (3 tests).

## Do not touch
Timesheet DTOs and billing flow.

## What I did (Claude, parallel agent)
Implemented as described; no schema change. dotnet build 0 errors; dotnet test backend/ERP.sln passes (253 unit, 289 integration, 4 skipped SQL Server).

## Review (Claude)
Self-implemented on branch claude/proj; build and tests green.
