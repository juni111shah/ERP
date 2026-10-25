---
id: TASK-008
title: Goods receipts post to a GR/IR clearing account and the bill clears it
priority: P0
status: in-progress
schema: no
depends_on: []
area: purchasing
audit_ref: AUDIT-DEEP B1, B11
commit:
---

## Problem
Stock receipts credit a clearing account (with fallbacks to AP control then inventory). When the vendor bill for the same PO is posted, AP is credited again and the clearing account is never cleared. GRN stock is received at PO price with no quantity tolerance check, and deleting a receipt does not reverse stock or journal.

## Impact
Liabilities double counted; clearing balance grows forever; deleted receipts leave stock behind.

## Change wanted
- Ensure a system role `inventory.clearing` exists in country/industry templates; remove the AP-control and inventory fallbacks (fail with a clear error if missing).
- `ConvertPurchaseOrderToBill`: bill lines for received items debit the clearing account (not expense/inventory again), with price differences to a variance account.
- GRN: reject quantities above ordered minus already received plus a workspace tolerance percent.
- Deleting or voiding a GRN reverses its stock movement and journal in one transaction; block when the PO is already billed.

## Acceptance criteria
- [ ] Receive then bill leaves clearing at zero and AP equal to the bill total only once.
- [ ] Over-receipt beyond tolerance returns 400.
- [ ] Deleting a receipt restores stock quantity and value and removes the journal effect.

## Tests required
`PurchasingLedgerTests` covering the three criteria.

## Do not touch
Requisition flow.

## What I did (Antigravity)

## Review (Claude)
