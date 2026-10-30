---
id: TASK-133
title: Purchase orders are editable only while Draft or Submitted and deletable only when nothing depends on them
priority: P1
status: ready-for-review
schema: no
depends_on: []
area: purchasing
audit_ref: Claude analysis, Procure to pay (second pass)
commit: 61e4493
owner: claude (branch claude/p2p; Antigravity skip)
---

## Problem
`UpdatePurchaseOrder` (backend/modules/ERP.Modules.Purchasing/Features/OrderFeatures.cs:196-224) blocks only `Billed`. Approved, PartiallyReceived, Received and Cancelled orders can be edited, and `PopulateLinesAndTotals` (line ~372) clears and recreates all lines, resetting `ReceivedQuantity`/`BilledQuantity` and orphaning `PurchaseOrderLineId` on goods receipts. The input legal entity is ignored. `DeletePurchaseOrder` (line ~227) blocks only `Billed`, so an order with goods receipts can be deleted.

## Impact
Receipt quantities and order status disagree with stock; receipts point to vanished lines; approved orders change without re-approval.

## Change wanted
- Update: only `Draft` or `Submitted` (409 otherwise); 400 on `legalEntityId` if it differs from the order's.
- Delete: only `Draft` or `Cancelled`, and only if no goods receipt references the order (409 otherwise).

## Acceptance criteria
- [x] Editing an Approved/PartiallyReceived/Received/Cancelled/Billed order returns 409 and changes nothing.
- [x] Changing the legal entity on update returns 400.
- [x] Deleting an Approved order or one with a goods receipt returns 409; deleting a Draft order works.

## Tests required
Integration tests for each rule on the purchasing test host.

## Do not touch
Status transition table (TASK-009).

## What I did (Claude, branch claude/p2p)
- Update only for Draft/Submitted (409), legal entity change is 400. Delete only Draft/Cancelled with no goods receipt (409).
- Also fixed an older bug found by the tests: updating a Draft purchase order returned 500 (Cross-workspace write blocked) because replaced lines carried keys and were tracked as existing rows. Lines now go through `IRepository<PurchaseOrderLine>` like invoices (commit 61e4493).
- Tests: `PurchaseOrderRulesTests`, including that a Draft/Submitted order can still be edited.

## Review (Claude)
