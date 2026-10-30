---
id: TASK-063
title: Van sales trips need a real route and catalog items with sensible quantities
priority: P1
status: done
schema: no
depends_on: []
area: distribution
audit_ref: Claude analysis, CRM and Distribution
commit: 943abbd
---

## Problem
`CreateTrip` (TripFeatures.cs) accepts any route id (a missing route silently becomes "Route"), skips the legal entity guard, trusts `ItemCode` and `ItemName` from the request, accepts item ids that are not in the catalog, duplicate items, zero or negative loaded quantities and empty driver or vehicle text. The existing test used `Guid.NewGuid()` as the item id.

## Impact
Trips reference routes and items that do not exist; completion later fails at stock issue or posts under made-up names.

## Change wanted
Guard the legal entity; the route must exist in that legal entity (400 on `routeId`); every item must exist in the catalog (400 on `items`) and its code and name come from the catalog; validator: driver and vehicle required, items unique, loaded quantity > 0, price >= 0. The warehouse is not checked because Distribution cannot reference the Inventory module.

## Acceptance criteria
- [x] Unknown route or legal entity returns 400.
- [x] Unknown item, zero quantity or a duplicate item returns 400.
- [x] The trip line carries the catalog SKU and name.

## Tests required
`DistributionTripTests.A_trip_needs_a_real_route_and_catalog_items_with_sensible_quantities`; the existing distribution exception test now creates a real item.

## Do not touch
Accounting and stock posting rules, other modules.

## What I did (Claude, on user request)
Implemented in commit 943abbd. `dotnet build backend/ERP.sln` 0 errors; unit tests 253 pass; integration tests 284 pass, 4 skipped (SQL Server).

## Review (Claude)
Self-implemented on request; verified by the build and test run above.
