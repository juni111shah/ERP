---
id: TASK-094
title: An inspection's lease must belong to the chosen unit
priority: P2
status: done
schema: no
depends_on: []
area: realestate
audit_ref: Claude analysis, Real estate
commit: 07f45ec
---

## Problem
`InspectionHandlers.MapAsync` (`InspectionFeatures.cs`) checks that the unit and the lease are each in the property, but not that the lease is for that unit.

## Impact
A move-out inspection of unit A can be attached to unit B's lease, so damage costs and work orders point at the wrong tenancy.

## Change wanted
When both `UnitId` and `LeaseId` are given, require `lease.UnitId == UnitId` (FieldValidationException `leaseId`).

## Acceptance criteria
- [x] A lease of another unit returns 400 on leaseId.
- [x] The matching lease is accepted.

## Tests required
`RealEstateBacklogTests.An_inspection_lease_must_belong_to_the_chosen_unit`.

## Do not touch
Inspection completion and work orders.

## What I did (Claude)
Implemented as described; see the commit.

## Review (Claude)
Self-implemented on user request. `dotnet build backend/ERP.sln` 0 errors; `dotnet test backend/ERP.sln` passes (SQL Server tests skipped locally).
