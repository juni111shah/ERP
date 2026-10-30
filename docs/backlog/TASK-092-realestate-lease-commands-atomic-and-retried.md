---
id: TASK-092
title: Lease creation with activation and lease renewal are atomic and retried
priority: P1
status: done
schema: no
depends_on: []
area: realestate
audit_ref: Claude analysis, Real estate
commit: HASH092
---

## Problem
`CreateLease` saves the lease and then calls `ActivateAsync` in a separate step (`LeaseFeatures.cs`), so a failed activation leaves a draft behind while the caller sees an error. `CreateLease` and `RenewLease` allocate a document number (`numbers.NextAsync`) but carry no `IRetryOnConcurrency` marker, unlike invoices and receipts (TASK-021).

## Impact
Two users creating or renewing leases at once can get a 409 on a simple save; a failed create-and-activate leaves an unexpected draft lease.

## Change wanted
Add `IRetryOnConcurrency` to `CreateLease` and `RenewLease`; run both handlers inside `unitOfWork.InTransactionAsync` (ActivateAsync joins the open transaction).

## Acceptance criteria
- [x] Creating a lease with activateNow on an unavailable unit fails and creates no lease.
- [x] Renewal works once and a second renewal is refused.
- [x] 10 parallel lease creations on different units all succeed with unique numbers (SQL Server host).

## Tests required
`RealEstateBacklogTests.Creating_with_activate_now_and_renewing_leave_one_consistent_lease_each` (SQLite); the parallel case is covered by the SQL Server collection and was not run locally.

## Do not touch
Numbering, rent schedule rules.

## What I did (Claude)
Implemented as described; see the commit.

## Review (Claude)
Self-implemented on user request. `dotnet build backend/ERP.sln` 0 errors; `dotnet test backend/ERP.sln` passes (SQL Server tests skipped locally).
