---
id: TASK-015
title: Add integration tests for Sales and Purchasing document flows
priority: P1
status: done
schema: no
depends_on: [TASK-009]
area: sales
audit_ref: AUD-040
commit: e083a4f
---

## Problem
No integration tests exist for Sales or Purchasing endpoints.

## Change wanted
Create `SalesFlowTests` and `PurchasingFlowTests` in `backend/tests/ERP.IntegrationTests` using `ApiFactory`: quote -> order -> fulfilment -> invoice; requisition -> PO -> receipt -> bill; tenant isolation (second workspace cannot read the first's documents); permission denial for a read-only role.

## Acceptance criteria
- [x] Both classes run green in `dotnet test`.
- [x] Each asserts stock movement and invoice/bill linkage, not only status codes.
- [x] Isolation and permission tests present.

## Tests required
Those above.

## Do not touch
Production code except to fix defects the tests expose; open a new card for each defect instead.

## What I did (Antigravity)
- Added `SalesFlowTests.cs` covering:
  - Full flow: quote creation -> quote acceptance -> conversion to sales order (confirmed) -> order fulfillment (issuing stock movement asserted) -> conversion to invoice (accounting invoice linked to order and verified in Finance).
  - Multi-tenant isolation: documents created in workspace A are inaccessible (404 and absent from paged lists) to workspace B (`demo-re`).
  - Permission denial: user assigned read-only role (`sales.quotes.read`, `sales.orders.read`) can query but receives 403 Forbidden on create, status updates, fulfillment, and conversion endpoints.
- Added `PurchasingFlowTests.cs` covering:
  - Full flow: requisition creation -> requisition approval -> conversion to purchase order -> order approval -> goods receipt creation (receiving stock movement asserted) -> conversion to bill (vendor bill linked to order and verified in Finance).
  - Multi-tenant isolation: documents created in workspace A are inaccessible to workspace B.
  - Permission denial: user assigned read-only role (`purchasing.orders.read`, `purchasing.requisitions.read`, `purchasing.receipts.read`) can query but receives 403 Forbidden on create, status updates, goods receipts, and conversion endpoints.
- Validated `dotnet test backend/ERP.sln` passing all 237 unit tests and all 225 integration tests.

## Review (Claude)

Verified 2026-10-30 on the merged branch in a clean `git archive` copy: `dotnet build` 0 errors, 238 unit and 236 integration tests passed (3 SQL Server tests skipped without ERP_TEST_SQL). Commit e083a4f adds only `SalesFlowTests.cs` and `PurchasingFlowTests.cs`; stock movement and invoice/bill linkage, tenant isolation and read-only role denial are asserted. Result: done.
