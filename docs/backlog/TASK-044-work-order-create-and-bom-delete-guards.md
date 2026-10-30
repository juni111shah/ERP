---
id: TASK-044
title: Work orders need an active BOM in the same entity and a valid warehouse; a BOM in use cannot be deleted
priority: P1
status: done
schema: no
depends_on: []
area: manufacturing
audit_ref: Claude analysis, Inventory and Manufacturing
commit: 600b559
---

## Problem
`CreateWorkOrderCommand` handling (`WorkOrderFeatures.cs`, `Handle(CreateWorkOrderCommand ...)`, around line 150) loads the BOM by id only: it does not check `bom.IsActive`, does not check that `request.LegalEntityId` equals `bom.LegalEntityId` (the order is stamped with the request's entity but costed from another entity's BOM), does not run `ILegalEntityGuard`, and never checks that `WarehouseId` exists, is active and belongs to the same legal entity. `DeleteBomCommand` (`BomFeatures.cs`) removes a BOM even when work orders reference it (`WorkOrder.BomId`).

Also found while testing: `UpdateBomCommand` (`BomFeatures.cs`) loaded the BOM with `Query(...)` (no tracking) and then called `SaveChangesAsync`, so a BOM update (name, `IsActive`, components) was returned in the response but never saved.

## Impact
Production against an inactive/obsolete BOM, work orders that mix legal entities, work orders for a non-existent warehouse that only fail later at release, and deleted BOMs leaving work orders whose `BomId` points nowhere (release and completion then throw NotFound).

## Change wanted
- In create: `ILegalEntityGuard.RequireAsync`; BOM must be active (`ConflictException("BOM <code> is inactive.")`) and `bom.LegalEntityId == request.LegalEntityId` (400 on `bomId`); warehouse must exist, be active and share the legal entity (400 on `warehouseId`; read `Warehouse` from the Inventory module through an existing abstraction if one exists, otherwise `IReadRepository<Warehouse>` is acceptable since both modules ship together).
- In `UpdateBomCommand` load the BOM with `Tracked(...)` so the changes persist.
- In `DeleteBomCommand`: `ConflictException("BOM is used by work orders and cannot be deleted. Deactivate it instead.")` when any work order references it.

## Acceptance criteria
- [ ] Creating a work order from an inactive BOM returns 409.
- [ ] A BOM of another legal entity, or an unknown/inactive/other-entity warehouse, returns 400.
- [ ] Deleting a BOM that has a work order returns 409; deleting an unused BOM still works.
- [ ] A BOM update (for example deactivating it) is saved and visible on the next read.
- [ ] Existing manufacturing tests pass.

## Tests required
`ManufacturingWorkflowTests`: the four cases above.

## Do not touch
Release, completion and cancel flows; costing.

## What I did (Claude, on user request)
CreateWorkOrder: legal entity guard, BOM must belong to the entity and be active, warehouse checked through new IStockPostingService.IsWarehouseUsableAsync (true when Inventory is off). DeleteBom returns 409 when work orders reference it. UpdateBom now loads with Tracked so changes persist (it silently saved nothing before).

## Review (Claude)
- Verified: dotnet build 0 errors; dotnet test 253 unit + 283 integration pass, 4 skipped (SQL Server).
