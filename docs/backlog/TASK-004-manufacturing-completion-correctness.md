---
id: TASK-004
title: Make work-order release/complete safe and post correct inventory journals
priority: P0
status: ready-for-review
schema: no
depends_on: []
area: manufacturing
audit_ref: AUDIT-DEEP B2, B3
commit: 3ce01c4
---

## Problem
`WorkOrderFeatures.cs` Release and Complete have no status guard, so completing twice posts stock and journals twice. `ActualTotalCost` is client-supplied. Completion receives finished goods (Dr Inventory, Cr clearing/AP) and then issues components to **COGS** (Dr COGS, Cr Inventory), expensing materials while also stocking finished goods.

## Impact
Double postings, overstated profit and assets, false liabilities.

## Change wanted
- Release only from `Planned`; Complete only from `InProgress`; Cancel only from `Planned`/`InProgress` with no completion. Wrong state returns `ConflictException`.
- Compute material cost from the components actually issued (stock average cost at issue time) plus the labour/overhead lines already on the work order; ignore a client `ActualTotalCost` or accept it only as an extra overhead amount with a validated maximum.
- Post one balanced journal group: Dr Finished-goods inventory, Cr Raw-material inventory (components) and Cr WIP/absorption for conversion cost. Components must **not** hit COGS.
- Use one transaction for status change, stock movements and journals.
- Reject completion if a BOM component item is missing or not a Product.

## Acceptance criteria
- [x] Second Complete call returns 409 and posts nothing.
- [x] After completion, trial balance still sums to zero and COGS is unchanged.
- [x] Finished-goods inventory value equals consumed material cost plus conversion cost.
- [x] Any failure rolls back the status change.

## Tests required
New `ManufacturingWorkflowTests` integration class covering the four criteria.

## Do not touch
BOM and costing report calculations.

## What I did (Antigravity)
- Guarded `ReleaseWorkOrderCommand` (must be `Planned`), `CancelWorkOrderCommand` (must be `Planned` or `InProgress` with 0 completed quantity), and `CompleteWorkOrderCommand` (must be `InProgress`); wrong state throws `ConflictException`.
- Handled tracking in handlers using `_orders.Tracked(...)` so entity status and completed quantity are persisted.
- Updated `IStockPostingService` and `StockReceiveRequest`/`StockIssueRequest` with optional `SkipJournal` flag and added `GetAverageCostAsync`.
- In `CompleteWorkOrderCommand`, calculated actual material cost using current inventory average costs, validated all BOM components exist and are `ItemType.Product`, issued raw materials and received finished goods with `SkipJournal: true`, and posted a single balanced journal (Dr Finished Goods Inventory, Cr Raw Material Inventory, Cr WIP/Absorption). Wrapped complete operation in `_unitOfWork.InTransactionAsync`.
- Added integration test suite `ManufacturingWorkflowTests` testing status guards, double completion rejection, trial balance balance, COGS neutrality, and transactional rollback on failure.

## Review (Claude)
