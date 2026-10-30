---
id: TASK-091
title: Lease needs a customer tenant, an existing tax code and an available unit
priority: P1
status: done
schema: no
depends_on: []
area: realestate
audit_ref: Claude analysis, Real estate
commit: 07f45ec
---

## Problem
`LeaseCommandHandlers.LoadContextAsync` (`LeaseFeatures.cs`) only checks that the tenant party exists; `Party.Roles` is never consulted and `LeaseInput.TaxCodeId` is not validated at all. `UpdateLease` does not re-check unit availability when the unit changes (only `CreateLease` does).

## Impact
A vendor or employee party can be set as tenant and billed rent; an unknown tax code id surfaces later as a database error or an invoice failure; a draft can be moved onto an occupied unit and only fails at activation.

## Change wanted
In `LoadContextAsync` reject a tenant whose roles are set but exclude Customer (FieldValidationException `lesseePartyId`), and a `TaxCodeId` that is not an active tax code (`taxCodeId`). In `UpdateLease`, when the unit changes, require the new unit to be Vacant or Reserved.

## Acceptance criteria
- [x] A vendor-only tenant returns 400 on lesseePartyId.
- [x] An unknown tax code returns 400 on taxCodeId.
- [x] A draft cannot be moved to an occupied unit (400 on unitId).
- [x] Parties with no roles or with Customer still work.

## Tests required
`RealEstateBacklogTests.A_lease_needs_a_customer_tenant_a_real_tax_code_and_a_free_unit`.

## Do not touch
Receivables, party CRUD.

## What I did (Claude)
Implemented as described; see the commit.

## Review (Claude)
Self-implemented on user request. `dotnet build backend/ERP.sln` 0 errors; `dotnet test backend/ERP.sln` passes (SQL Server tests skipped locally).
