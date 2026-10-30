---
id: TASK-042
title: A warehouse that still holds stock cannot be deactivated
priority: P2
status: done
schema: no
depends_on: []
area: inventory
audit_ref: Claude analysis, Inventory and Manufacturing
commit: bb31e5c
---

## Problem
`UpdateWarehouse` (`WarehouseFeatures.cs`) sets `IsActive` from the input with no check. `StockHandlers.RequireAsync` then rejects every movement for an inactive warehouse (`Warehouse {code} is not active`), including the issue or transfer that would empty it.

## Impact
Stock (and its ledger value) becomes stranded in a warehouse nobody can move it out of; the user must re-activate the warehouse first.

## Change wanted
- In `UpdateWarehouse`, when `IsActive` goes from true to false, replay the movements of that warehouse (use `StockCosting.Replay` per item, or group quantities in memory) and throw `ConflictException("Warehouse <code> still holds stock. Move or issue it first.")` when any item has a non-zero quantity.
- Reactivating and renaming stay unchanged.

## Acceptance criteria
- [ ] Deactivating a warehouse with 5 units on hand returns 409 and the warehouse stays active.
- [ ] After the stock is transferred out, deactivation succeeds.
- [ ] Renaming a warehouse that holds stock still works.

## Tests required
`InventoryTests`: the three cases.

## Do not touch
Warehouse create, code uniqueness.

## What I did (Claude, on user request)
UpdateWarehouse replays the warehouse's movements and returns 409 when deactivating while any item has a non-zero quantity. Test covers rename with stock, blocked deactivation, deactivation after a transfer out.

## Review (Claude)
- Verified: dotnet build 0 errors; dotnet test 253 unit + 283 integration pass, 4 skipped (SQL Server).
