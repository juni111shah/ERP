---
id: TASK-050
title: Tasks stay in their project and follow the status flow
priority: P1
status: done
schema: no
depends_on: []
area: projects
audit_ref: Claude analysis, Projects and Services
commit: HASH
---

## Problem
TaskFeatures.cs (CreateTask/UpdateTask): `MilestoneId` is stored without checking it belongs to the task project; `UpdateTask` assigns `Status` directly so a task can jump Todo -> Done or Done -> Todo, bypassing `ProjectsTransitions.Task` that `ChangeTaskStatus` enforces; tasks can be created on Completed or Cancelled projects.

## Impact
Milestone progress and project reporting mix tasks from other projects; the status flow is optional; closed projects keep growing.

## Change wanted
Validate the milestone belongs to the project (400 on `milestoneId`); in `UpdateTask` call `ProjectsTransitions.Task.Ensure` when the status changes; add `ProjectRules.EnsureOpen` (409 for Completed/Cancelled) to `CreateTask`.

## Acceptance criteria
- [x] A task with a milestone of another project returns 400 on create and update.
- [x] Updating a task from Todo to Done returns 409; same-status edits succeed.
- [x] Creating a task on a completed project returns 409.

## Tests required
ProjectTaskRulesTests (2 tests).

## Do not touch
Task DTOs, endpoints, ChangeTaskStatus.

## What I did (Claude, parallel agent)
Implemented as described; no schema change. dotnet build 0 errors; dotnet test backend/ERP.sln passes (253 unit, 289 integration, 4 skipped SQL Server).

## Review (Claude)
Self-implemented on branch claude/proj; build and tests green.
