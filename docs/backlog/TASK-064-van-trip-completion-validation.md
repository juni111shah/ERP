---
id: TASK-064
title: Trip completion validates the reconciliation against what was loaded
priority: P1
status: done
schema: no
depends_on: []
area: distribution
audit_ref: Claude analysis, CRM and Distribution
commit: efae3e5
---

## Problem
`CompleteTrip` has no validator. Quantities can be negative, `sold + returned + damaged` may exceed the loaded quantity (stock issued for goods never loaded), lines for items not on the trip are silently ignored, totals may be negative and a cancelled trip can be completed.

## Impact
Wrong stock issues and trip totals; typos are accepted silently.

## Change wanted
`CompleteTripValidator` (totals >= 0 and two decimals, quantities >= 0, unique lines). In the handler: reject cancelled trips (409), unknown lines and `sold + returned + damaged > loaded` (400 on `items`) before anything is saved.

## Acceptance criteria
- [x] Over-reconciled, negative, unknown or duplicate lines return 400 and the trip stays in progress.
- [x] A valid reconciliation completes and stores the quantities.

## Tests required
`DistributionTripTests.A_trip_completes_only_with_a_reconciliation_that_matches_what_was_loaded`.

## Do not touch
Accounting and stock posting rules, other modules.

## What I did (Claude, on user request)
Implemented in commit efae3e5. `dotnet build backend/ERP.sln` 0 errors; unit tests 253 pass; integration tests 284 pass, 4 skipped (SQL Server).

## Review (Claude)
Self-implemented on request; verified by the build and test run above.
