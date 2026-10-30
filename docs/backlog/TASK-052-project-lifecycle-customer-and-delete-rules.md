---
id: TASK-052
title: Project status, customer role and delete are guarded
priority: P1
status: done
schema: no
depends_on: []
area: projects
audit_ref: Claude analysis, Projects and Services
commit: 8131d17
---

## Problem
ProjectFeatures.cs: `Map` sets `Status` from the input on both create and update, so an edit can reopen a Completed project or create one as Completed, bypassing `ProjectsTransitions.Project`; `CustomerId` is never validated (any party, even a vendor); `DeleteProject` removes a project whose timesheets were invoiced.

## Impact
Invoiced work loses its project, completed projects reopen silently, and vendors can be billed as customers.

## Change wanted
Create accepts only Planning or Active (400 on `status`); `UpdateProject` calls `ProjectsTransitions.Project.Ensure` when the status changes; `EnsureCustomerAsync` rejects unknown parties and parties with roles that lack Customer (400 on `customerId`); delete returns 409 when any billed timesheet or Invoiced milestone exists.

## Acceptance criteria
- [x] Creating a project as Completed returns 400; Planning -> Completed by edit returns 409.
- [x] A vendor-only party as customer returns 400; unknown id returns 400.
- [x] Deleting a project with invoiced work returns 409; a clean project deletes.

## Tests required
ProjectLifecycleRulesTests (3 tests).

## Do not touch
Project DTOs, custom fields, the status endpoint.

## What I did (Claude, parallel agent)
Implemented as described; no schema change. dotnet build 0 errors; dotnet test backend/ERP.sln passes (253 unit, 289 integration, 4 skipped SQL Server).

## Review (Claude)
Self-implemented on branch claude/proj; build and tests green.
