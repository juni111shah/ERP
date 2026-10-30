---
id: TASK-062
title: Opportunity close follows the pipeline; closed stages force probability; reopening clears the close
priority: P1
status: done
schema: no
depends_on: []
area: crm
audit_ref: Claude analysis, CRM and Distribution
commit: 3245b91
---

## Problem
`CreateOpportunity` accepts `Stage = ClosedWon/ClosedLost`. `CloseOpportunity` sets the stage directly, bypassing `CrmTransitions` (a deal could be won from Qualification). `UpdateOpportunityStage` keeps the request's probability for closed stages (a lost deal at 55%) and, when a lost deal is reopened, leaves `ActualCloseDate` and `CloseReason` behind.

## Impact
Pipeline and win-rate reporting is wrong; closed deals carry open-deal probabilities.

## Change wanted
Reject closed stages on create (400 on `stage`). `CloseOpportunity` goes through `CrmTransitions.Opportunity.Ensure`. ClosedWon forces 100%, ClosedLost 0%. Leaving a closed stage clears `ActualCloseDate`, `ActualRevenue` and `CloseReason`.

## Acceptance criteria
- [x] Creating a closed opportunity returns 400.
- [x] Closing as won from Qualification returns 409; from Negotiation succeeds with 100%.
- [x] Moving to ClosedLost with probability 55 stores 0.
- [x] A reopened opportunity has no actual close date or revenue.

## Tests required
`CrmModuleTests.Opportunity_close_follows_the_pipeline_and_reopening_clears_the_close`; two existing tests adjusted (closing as lost instead of won straight from Qualification).

## Do not touch
Accounting and stock posting rules, other modules.

## What I did (Claude, on user request)
Implemented in commit 3245b91. `dotnet build backend/ERP.sln` 0 errors; unit tests 253 pass; integration tests 284 pass, 4 skipped (SQL Server).

## Review (Claude)
Self-implemented on request; verified by the build and test run above.
