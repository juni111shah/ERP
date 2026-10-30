---
id: TASK-060
title: CRM records check legal entity, customer and related record; interactions record the caller
priority: P1
status: done
schema: no
depends_on: []
area: crm
audit_ref: Claude analysis, CRM and Distribution
commit: e80dd84
---

## Problem
`CreateLead` (LeadFeatures.cs), `CreateOpportunity` (OpportunityFeatures.cs) and `LogInteraction` (InteractionFeatures.cs) never call `ILegalEntityGuard`, so any legal entity id is accepted. `CustomerPartyId` of an opportunity is not checked. An interaction's `RelatedEntityKind` is free text and `RelatedEntityId` is never looked up. `PerformedByUserId` and `PerformedByName` come from the request, so a user can log activity as someone else.

## Impact
Records attach to entities or parties that do not exist or that the caller may not use; the activity log can be forged.

## Change wanted
Call the guard in the three create handlers; reject a missing `CustomerPartyId` (field `customerPartyId`); limit `RelatedEntityKind` to Lead, Opportunity, Customer (case-insensitive, stored canonical) and require the related record to exist (leads and opportunities in the same legal entity); record `ICurrentUser.UserId` and email as the performer.

## Acceptance criteria
- [x] Unknown legal entity returns 400 for lead, opportunity and interaction.
- [x] Unknown customer returns 400 on `customerPartyId`.
- [x] Unknown kind or unknown related id returns 400.
- [x] The performer is the signed-in user whatever the request says.

## Tests required
`CrmModuleTests.Crm_records_need_a_real_legal_entity_customer_and_related_record`.

## Do not touch
Accounting and stock posting rules, other modules.

## What I did (Claude, on user request)
Implemented in commit e80dd84. `dotnet build backend/ERP.sln` 0 errors; unit tests 253 pass; integration tests 284 pass, 4 skipped (SQL Server).

## Review (Claude)
Self-implemented on request; verified by the build and test run above.
