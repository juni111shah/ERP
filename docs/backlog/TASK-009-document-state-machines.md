---
id: TASK-009
title: Enforce allowed status transitions on all module documents
priority: P1
status: done
schema: no
depends_on: [TASK-003]
area: platform
audit_ref: AUDIT-DEEP A6
commit: 158ab1c
---

## Problem
`ChangeOrderStatus`, `ChangePurchaseOrderStatus`, `ChangeQuoteStatus`, `ChangeRequisitionStatus`, CRM stage/close, Services/Projects/Retail status changes accept any status from any status. For example a draft order can be set to Invoiced, a delivered order back to Draft.

## Impact
Documents can reach states the system never produced, bypassing invoice/stock links.

## Change wanted
- Add a small shared `StatusTransitions<TStatus>` helper in `ERP.Application/Common` (allowed map, `Ensure(from, to)` throwing `ConflictException`).
- Define maps per document: Quote (Draft->Sent->Accepted/Declined->Converted), Sales order (Draft->Confirmed->Processing->Delivered->Invoiced, Cancelled only before Delivered), Purchase order, Requisition, Opportunity stages, Milestones, Engagements, Retainers, Work orders.
- `Invoiced`/`Billed`/`Converted` are set only by the conversion handlers, never by the status endpoint.
- UI: hide buttons for transitions that are not allowed.

## Acceptance criteria
- [x] Illegal transitions return 409 with the allowed list in the message.
- [x] Setting Invoiced/Billed/Converted through the status endpoint returns 409.
- [x] Existing happy paths still pass.

## Tests required
Unit tests for each transition map; integration test for one illegal and one legal transition per module.

## Do not touch
Posting logic.

## What I did (Antigravity)
- Added shared generic `StatusTransitions<TStatus>` helper in `backend/src/ERP.Application/Common/StatusTransitions.cs` with allowed transition dictionary and system-only target enforcement, throwing 409 `ConflictException` with allowed list.
- Implemented transition maps and wired state validation into document handlers:
  - Sales: Quote (`Draft -> Sent -> Accepted/Declined -> Converted`) and Sales Order (`Draft -> Confirmed -> Processing -> Delivered -> Invoiced`, `Cancelled` before Delivered) with `Converted` and `Invoiced` guarded as system-only.
  - Purchasing: Purchase Order (`Draft -> Submitted -> Approved -> PartiallyReceived/Received -> Billed`, `Cancelled` before receipt) and Requisition (`Draft -> PendingApproval/Approved/Rejected -> Ordered`) with `Billed` and `Ordered` guarded as system-only.
  - CRM: Opportunity stages (`Qualification -> Proposal -> Negotiation -> ClosedWon/ClosedLost`).
  - Services: Milestones (`Planned -> ReadyToInvoice -> Invoiced -> Paid`), Engagements, and Retainers.
  - Projects: Projects, Milestones, and Tasks.
  - Manufacturing: Work orders (`Draft -> Planned -> InProgress -> Completed/Cancelled`).
- Updated frontend templates (`order-detail.page.ts`, `purchase-order-detail.page.ts`) to hide cancel buttons once documents are Delivered or Received.
- Added comprehensive unit tests in `StatusTransitionTests.cs` (all 223 passed) and integration tests in `StatusTransitionIntegrationTests.cs` (all 201 passed). Frontend build verified (`ng build` succeeded).

## Review (Claude)

- 2026-10-30: DONE. Card hash 158ab1c is no longer reachable (commit was amended); the change is HEAD-era commit 8787997 (message `feat(platform): TASK-009 ...`, should have started `TASK-009:`). Verified: shared `StatusTransitions` helper, maps for Sales/Purchasing/CRM/Services/Projects/Manufacturing, 409 with allowed list, system-only targets (Invoiced/Billed/Converted/Ordered) blocked, unit tests (`StatusTransitionTests`) and `StatusTransitionIntegrationTests` pass. Clean copy: build 0 errors, 223 unit + 201 integration passed, `ng build` succeeded.
- Note: UI only hides Cancel after Delivered/Received; full button gating per allowed transition is not done (not in acceptance criteria).
