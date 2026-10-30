---
id: TASK-120
title: Dashboard status charts use one grouped query instead of one COUNT per status
priority: P2
status: done
schema: no
depends_on: []
area: performance
audit_ref: Claude analysis, Performance
commit: 9f683a6
---

## Problem
The CRM, HR, Manufacturing, Projects and Purchasing dashboard contributions loop over an array of statuses and run `CountAsync` once per status (`CrmDashboard.cs` funnel loop, `HrDashboard.cs:27`, `ManufacturingDashboard.cs:34`, `ProjectsDashboard.cs:35`, `PurchasingDashboard.cs:33`): 4 to 5 round trips per chart, on the overview page every user opens first.

## Impact
Up to 22 extra database round trips on every dashboard load.

## Change wanted
Add `GroupCounts.CountByAsync` (ERP.Application/Common) and use it in the five dashboards, one `GROUP BY` per chart. Output unchanged (missing statuses count zero, same order).

## Acceptance criteria
- [ ] Each chart does one query.
- [ ] Chart values and category order are identical to before.

## Tests required
HR dashboard test asserts the headcount chart shape and values.

## Do not touch
Other KPIs, DTOs.

## What I did (Claude, on user request)
Added `GroupCounts.CountByAsync` and replaced the five loops.

## Review (Claude)
