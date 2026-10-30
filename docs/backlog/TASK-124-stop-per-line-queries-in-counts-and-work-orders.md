---
id: TASK-124
title: Stock count save/post and work-order completion stop querying once per line
priority: P2
status: done
schema: no
depends_on: []
area: performance
audit_ref: Claude analysis, Performance
commit:
---

## Problem
- `SaveCountedQuantities` (`ERP.Modules.Inventory/Features/StockCountFeatures.cs`) calls `items.FindAsync` for every new line.
- `PostStockCount` loads the full movement history of the warehouse for each count line (one query per line) just to replay one item's position.
- `CompleteWorkOrderCommand` (`ERP.Modules.Manufacturing/Features/WorkOrderFeatures.cs:240-246`) calls `_items.FindAsync` for every BOM component.

## Impact
Round trips grow with the number of lines or components; a 500-line count sheet runs about 1000 queries on save and post.

## Change wanted
Resolve all new items in one query; load the warehouse movements for all lines in one query and group by item before replaying; load all component item types in one query. Behaviour unchanged, except that a repeated new item in one save now produces one line (the later quantity wins) instead of two.

## Acceptance criteria
- [ ] Same results and error messages.
- [ ] One query for items per save, one for movements per post, one for component types per completion.

## Tests required
`InventoryDepthTests`: several new items in one save, a repeated item, an unknown item (400), post and variances. Existing count and manufacturing tests pass.

## Do not touch
Costing, journals.

## What I did (Claude, on user request)
Replaced the three loops with batched queries.

## Review (Claude)
