---
id: TASK-015
title: Add integration tests for Sales and Purchasing document flows
priority: P1
status: open
schema: no
depends_on: [TASK-009]
area: sales
audit_ref: AUD-040
commit:
---

## Problem
No integration tests exist for Sales or Purchasing endpoints.

## Change wanted
Create `SalesFlowTests` and `PurchasingFlowTests` in `backend/tests/ERP.IntegrationTests` using `ApiFactory`: quote -> order -> fulfilment -> invoice; requisition -> PO -> receipt -> bill; tenant isolation (second workspace cannot read the first's documents); permission denial for a read-only role.

## Acceptance criteria
- [ ] Both classes run green in `dotnet test`.
- [ ] Each asserts stock movement and invoice/bill linkage, not only status codes.
- [ ] Isolation and permission tests present.

## Tests required
Those above.

## Do not touch
Production code except to fix defects the tests expose; open a new card for each defect instead.

## What I did (Antigravity)

## Review (Claude)
