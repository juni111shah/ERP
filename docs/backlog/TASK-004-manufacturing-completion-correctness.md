---
id: TASK-004
title: Make work-order release/complete safe and post correct inventory journals
priority: P0
status: open
schema: no
depends_on: []
area: manufacturing
audit_ref: AUDIT-DEEP B2, B3
commit:
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
- [ ] Second Complete call returns 409 and posts nothing.
- [ ] After completion, trial balance still sums to zero and COGS is unchanged.
- [ ] Finished-goods inventory value equals consumed material cost plus conversion cost.
- [ ] Any failure rolls back the status change.

## Tests required
New `ManufacturingWorkflowTests` integration class covering the four criteria.

## Do not touch
BOM and costing report calculations.

## What I did (Antigravity)

## Review (Claude)
