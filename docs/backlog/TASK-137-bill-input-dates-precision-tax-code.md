---
id: TASK-137
title: Bill input validates dates, quantity/price precision and active tax codes
priority: P2
status: in-progress
schema: no
depends_on: []
area: purchasing
audit_ref: Claude analysis, Procure to pay (second pass)
commit:
owner: claude (branch claude/p2p; Antigravity skip)
---

## Problem
`BillInputValidator` (backend/src/ERP.Application/Finance/BillFeatures.cs:47-65) has no rule for `BillDate`/`DueDate` (year 1 allowed) and no decimal limit for `Quantity` and `Price`. `ApplyAsync` loads tax codes without checking `IsActive` (line 214). This is the payables twin of TASK-129.

## Impact
Bills dated in a non-existent period, drifting line totals, tax booked with a withdrawn code.

## Change wanted
- `BillDate` and `DueDate` use `MustBePlausibleDate()`; due date not before bill date as a validator rule (key `dueDate`).
- Quantity and Price `MustHaveAtMostDecimals(4)`.
- `ApplyAsync` accepts only active tax codes.

## Acceptance criteria
- [ ] Bill dated 0001-01-01 or 2099 returns 400 on `billDate`; due before bill date returns 400 on `dueDate`.
- [ ] Quantity 1.00001 and price 9.12345 rejected; 1.2345 accepted.
- [ ] An inactive tax code returns 400 on `lines`.

## Tests required
Validator unit tests plus one integration test per rule.

## Do not touch
Posting logic and Net/Tax rounding.

## What I did (Antigravity)

## Review (Claude)
