---
id: TASK-007
title: Post POS sales and tax to the ledger
priority: P0
status: open
schema: no
depends_on: [TASK-005, TASK-006]
area: pos
audit_ref: AUDIT-DEEP B5
commit:
---

## Problem
POS orders post only COGS/inventory through stock issue. Revenue, output tax and cash/card receipts never reach the ledger, so P&L, VAT and bank/cash reports exclude POS sales.

## Impact
Financial statements are wrong for any workspace using POS.

## Change wanted
- On order completion call `IPostingService` with: Dr Cash (or Card clearing per payment method) for each payment, Cr Sales revenue for the net amount, Cr Output tax for the tax per tax code. Account lookup through system roles (add `pos.cash`, `pos.card-clearing` roles if missing, fall back to existing cash/bank roles).
- Void posts the exact reversal.
- Put the posting in the same transaction as the order.
- Journal source references the POS order id so the journal screen can link back.

## Acceptance criteria
- [ ] After one cash sale the trial balance is balanced and revenue and tax accounts show the amounts.
- [ ] Mixed cash/card payment posts to the right accounts.
- [ ] Void reverses revenue, tax and cash exactly.
- [ ] Journal source link opens the POS order.

## Tests required
`PosLedgerTests` integration tests for the four criteria.

## Do not touch
Invoice and receipt modules.

## What I did (Antigravity)

## Review (Claude)
