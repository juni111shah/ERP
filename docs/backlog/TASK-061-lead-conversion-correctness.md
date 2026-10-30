---
id: TASK-061
title: Lead conversion works, is atomic and happens once; converted leads are frozen
priority: P0
status: done
schema: no
depends_on: []
area: crm
audit_ref: Claude analysis, CRM and Distribution
commit: 503e083
---

## Problem
`ConvertLead` asked for number sequence `CUST` (LeadFeatures.cs), which no module or template declares, so every conversion failed with 409 "No number sequence is configured for 'CUST'". It also saved the party, the opportunity and the lead in three separate saves, could be run again on a converted lead (second opportunity), and `UpdateLead` let any caller set `Status = Converted` or edit a converted lead.

## Impact
Converting a lead, the main CRM flow, did not work; if it had, it could leave a customer without an opportunity or create duplicates.

## Change wanted
Take the customer code from the core `party` sequence. Run the conversion in `IUnitOfWork.InTransactionAsync`. Reject converting an already converted lead (409) or an Unqualified one (409). `UpdateLead` rejects `Status = Converted` (400 on `status`) and any edit of a converted lead (409).

## Acceptance criteria
- [x] A lead converts and links party and opportunity.
- [x] A second conversion returns 409 and changes nothing.
- [x] Updating a lead to Converted returns 400; updating a converted lead returns 409.
- [x] An unqualified lead converts only after it is requalified.

## Tests required
`CrmModuleTests.A_lead_converts_once_and_a_converted_lead_is_frozen`, `An_unqualified_lead_cannot_be_converted_until_it_is_requalified`.

## Do not touch
Accounting and stock posting rules, other modules.

## What I did (Claude, on user request)
Implemented in commit 503e083. `dotnet build backend/ERP.sln` 0 errors; unit tests 253 pass; integration tests 284 pass, 4 skipped (SQL Server).

## Review (Claude)
Self-implemented on request; verified by the build and test run above.
