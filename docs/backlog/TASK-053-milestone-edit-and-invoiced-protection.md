---
id: TASK-053
title: Milestones: editable when pending, immutable when invoiced, sane dates
priority: P1
status: done
schema: no
depends_on: []
area: projects
audit_ref: Claude analysis, Projects and Services
commit: HASH
---

## Problem
MilestoneFeatures.cs: `UpdateMilestone` calls `Ensure(m.Status, i.Status)` even when the status is unchanged, which has no Pending->Pending entry so a plain edit of a pending milestone returns 409; an Invoiced milestone can be edited (amount) or deleted; `CreateMilestone` accepts Status Invoiced; `AchievedDate` can be in the future or set on Pending milestones.

## Impact
Editing a pending milestone is impossible, and invoiced milestones can be changed after billing so the invoice and project disagree.

## Change wanted
Run the transition check only when the status changes; reject edit and delete of Invoiced milestones (409); reject Invoiced on create (400 on `status`); clear the date for Pending, default to today for Achieved and reject future dates (400 on `achievedDate`), also in `AchieveMilestone`; closed projects take no new milestones.

## Acceptance criteria
- [x] Renaming a pending milestone succeeds.
- [x] An invoiced milestone returns 409 on edit and delete.
- [x] Creating a milestone as Invoiced returns 400.
- [x] An achieved date in 2099 returns 400.

## Tests required
ProjectMilestoneRulesTests (3 tests).

## Do not touch
Milestone DTOs and the billing conversion to Invoiced.

## What I did (Claude, parallel agent)
Implemented as described; no schema change. dotnet build 0 errors; dotnet test backend/ERP.sln passes (253 unit, 289 integration, 4 skipped SQL Server).

## Review (Claude)
Self-implemented on branch claude/proj; build and tests green.
