---
id: TASK-121
title: Project billing overview reads only needed columns and groups once
priority: P2
status: done
schema: no
depends_on: []
area: performance
audit_ref: Claude analysis, Performance
commit: 06152f4
---

## Problem
`GetBillingOverview` (`ERP.Modules.Projects/Features/BillingFeatures.cs`, handler `Handle(GetBillingOverview)`) loads every project, every unbilled timesheet and every achieved milestone as full entities, then for each project re-scans both lists with `Where(x => x.ProjectId == p.Id)` (O(projects x rows)).

## Impact
The overview screen slows quadratically as projects and timesheets grow, and transfers whole rows it never shows.

## Change wanted
Project only `ProjectId`, `Hours`, `TotalAmount` / `Amount`, group into dictionaries once, and load only projects that are Active or have something billable. Output and ordering unchanged. (Sums stay client-side: SQLite cannot `Sum` decimals.)

## Acceptance criteria
- [ ] Same rows and totals as before.
- [ ] Inactive projects with nothing billable are not loaded or returned.

## Tests required
`ProjectsBillingTests`: overview totals for a busy project (two timesheets) and absence of an idle on-hold project.

## Do not touch
`BillProject`, DTO shape.

## What I did (Claude, on user request)
Rewrote the handler with column projections and dictionaries.

## Review (Claude)
