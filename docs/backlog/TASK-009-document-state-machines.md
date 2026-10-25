---
id: TASK-009
title: Enforce allowed status transitions on all module documents
priority: P1
status: open
schema: no
depends_on: [TASK-003]
area: platform
audit_ref: AUDIT-DEEP A6
commit:
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
- [ ] Illegal transitions return 409 with the allowed list in the message.
- [ ] Setting Invoiced/Billed/Converted through the status endpoint returns 409.
- [ ] Existing happy paths still pass.

## Tests required
Unit tests for each transition map; integration test for one illegal and one legal transition per module.

## Do not touch
Posting logic.

## What I did (Antigravity)

## Review (Claude)
