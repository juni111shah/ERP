---
id: TASK-043
title: Validate BOM components, items and duplicate codes
priority: P1
status: done
schema: no
depends_on: []
area: manufacturing
audit_ref: Claude analysis, Inventory and Manufacturing
commit: 40b3663
---

## Problem
`CreateBomValidator`/`UpdateBomValidator` (`backend/modules/ERP.Modules.Manufacturing/Features/BomFeatures.cs`) validate only the header. The handlers trust the client for everything else: `QuantityRequired`, `ScrapPercentage` and `UnitCost` have no rules (a quantity of -5 or scrap of 500% is saved and flows into `Map` and work-order cost), component and produced item ids are never checked against the catalog (`ProducedItemCode`, `ComponentItemName` are free text from the client), the legal entity is never passed through `ILegalEntityGuard`, a BOM can list its own produced item as a component, the same component can appear twice, and a duplicate `Code` surfaces as a database unique-index error (500) instead of a conflict.

## Impact
Negative or absurd component quantities corrupt work-order cost and the stock issued at release; BOMs reference items that do not exist or are services; duplicate codes give an unhelpful 500.

## Change wanted
- Component validator: `QuantityRequired > 0` (max 4 decimals), `ScrapPercentage` between 0 and 100, `UnitCost >= 0`, `ComponentItemId` not empty, no duplicate `ComponentItemId` in one BOM, and no component equal to `ProducedItemId` (create) / the BOM's produced item (update).
- Handler: `ILegalEntityGuard.RequireAsync(LegalEntityId)` on create; load the produced and component items from `IReadRepository<Item>`: they must exist and be `ItemType.Product` (400 on `producedItemId` / `components`); overwrite `ProducedItemCode/Name` and each component code/name from the catalog (`Item.Sku`, `Item.Name`) instead of trusting the request.
- Duplicate code (case-insensitive, the code is upper-cased) returns `ConflictException` before saving.

## Acceptance criteria
- [ ] Quantity <= 0, scrap > 100, negative cost, duplicate component, self-component return 400.
- [ ] A service item or unknown item id returns 400.
- [ ] Stored codes and names come from the catalog even if the request sends different text.
- [ ] Creating a second BOM with the same code returns 409.
- [ ] Existing manufacturing tests pass.

## Tests required
`ManufacturingWorkflowTests`: one test for the validation rules, one for catalog names, one for the duplicate code.

## Do not touch
Work-order flows, costing queries.

## What I did (Claude, on user request)
BomComponentInputValidator plus duplicate/self-component rules; BomHandlers checks the legal entity, loads produced and component items (must be products) and takes sku/name from the catalog, returns 409 for a duplicate code (case-insensitive). Existing test Failure_rolls_back_status_change_when_component_is_not_product now turns a product into a service via PUT /items before the work order, since a service BOM can no longer be created.

## Review (Claude)
- Verified: dotnet build 0 errors; dotnet test 253 unit + 283 integration pass, 4 skipped (SQL Server).
