---
id: TASK-007
title: Post POS sales and tax to the ledger
priority: P0
status: done
schema: no
depends_on: [TASK-005, TASK-006]
area: pos
audit_ref: AUDIT-DEEP B5
commit: c39b5de
owner: claude (worktree D:/ERP/work-claude, branch claude/impl; Antigravity skip)
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
- [x] After one cash sale the trial balance is balanced and revenue and tax accounts show the amounts.
- [x] Mixed cash/card payment posts to the right accounts.
- [x] Void reverses revenue, tax and cash exactly.
- [x] Journal source link opens the POS order.

## Tests required
`PosLedgerTests` integration tests for the four criteria.

## Do not touch
Invoice and receipt modules.

## What I did (Antigravity)

## Review (Claude)

Implemented by Claude in worktree D:/ERP/work-claude (branch claude/impl). Verified 2026-10-30: dotnet build 0 errors; unit + integration suite passes (238 unit, 230 integration passed, 3 SQL Server tests skipped without ERP_TEST_SQL and passed on LocalDB).
- POS create now posts Dr cash/card (roles pos.cash, pos.card-clearing with fallback to cash, bank), Dr receivables for any unpaid balance, Cr sales revenue, Cr output tax, in the order's transaction; a posting failure rolls the sale back (the old swallowing try/catch is gone). Void reverses the journal. Deviation: tax is posted to the single output-tax account, because tax codes have no per-code account. Tests: PosLedgerTests (cash sale, mixed tender, void, journal source). Note: the journal list DTO has no sourceId, so the link back to the order is by sourceType and memo only.
