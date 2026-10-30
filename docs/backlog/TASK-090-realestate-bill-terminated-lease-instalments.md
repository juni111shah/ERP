---
id: TASK-090
title: Invoice the pro-rated instalment of a terminated, ended or renewed lease
priority: P0
status: done
schema: no
depends_on: []
area: realestate
audit_ref: Claude analysis, Real estate
commit: HASH090
---

## Problem
`BillingHandlers.TryInvoiceAsync` (`BillingFeatures.cs`) returns null when `!lease.IsOccupying`. `TerminateLease` (`LeaseFeatures.cs`) pro-rates the instalment the termination date falls in and leaves it `Scheduled`; leases that `Expire` to Ended or are Renewed likewise keep unbilled instalments. Because those leases are not occupying, `RunBilling` silently skips them and `InvoiceInstalment` answers "lease is not active".

## Impact
Rent already earned (the used days of the last period, or any instalment left unbilled when a lease ended or was renewed) can never be invoiced: lost revenue with no error.

## Change wanted
In `TryInvoiceAsync` bill instalments of leases in any status except Draft and Cancelled. Update the `InvoiceInstalment` message accordingly.

## Acceptance criteria
- [x] Terminating a lease mid-period and running billing as of a later date invoices the pro-rated amount of that instalment.
- [x] Instalments cancelled by the termination are never invoiced.
- [x] Existing real estate tests pass.

## Tests required
`RealEstateBacklogTests.The_pro_rated_instalment_of_a_terminated_lease_can_still_be_invoiced`.

## Do not touch
Termination credit notes, schedule generation.

## What I did (Claude)
Implemented as described; see the commit.

## Review (Claude)
Self-implemented on user request. `dotnet build backend/ERP.sln` 0 errors; `dotnet test backend/ERP.sln` passes (SQL Server tests skipped locally).
