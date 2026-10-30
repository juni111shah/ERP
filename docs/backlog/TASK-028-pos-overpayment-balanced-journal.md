---
id: TASK-028
title: POS rejects or caps overpayment so the sale journal always balances
priority: P0
status: open
schema: no
depends_on: []
area: pos
audit_ref: TASK-005 review follow-up
commit:
---

## Problem
`CreatePosOrderCommand` only rejects `paymentsSum < totalAmount` (`OrderFeatures.cs:361-368`). When `paymentsSum > totalAmount` (for example a cash line with `Amount` 150 and `TenderedAmount` 150 on a 100 order, or two card lines totalling more than the order) the order is stored as `Paid`. The ledger entry debits every `p.Amount` (`OrderFeatures.cs:445-449`) but credits only `subtotal + taxTotal` (`455-459`), so the journal is unbalanced: the posting fails or the books drift. `session.ExpectedCash` is also increased by the overpaid cash (`417-431`).

## Impact
A legitimate "customer hands 150, pay 100" flow either fails with a posting error or books cash that was never revenue. Cash drawer expectation is wrong.

## Change wanted
In `OrderHandlers.Handle(CreatePosOrderCommand)` after computing `totalAmount`:
- `Amount` is the amount applied to the order. Reject (400, field `payments`) when `paymentsSum > totalAmount`. Change is expressed only through `TenderedAmount`/`ChangeAmount` on cash lines (`Amount` = applied, `TenderedAmount` = handed over, `ChangeAmount` = tendered - amount).
- Reject a non-cash line whose `Amount` pushes the running sum above the total.
- Keep the server-computed `ChangeAmount` (already the case).
- Keep partial-payment behaviour unchanged (`pos.allowPartialPayment`).

## Acceptance criteria
- [ ] Cash `Amount` 150 on a 100 order returns 400.
- [ ] Cash `Amount` 100 with `TenderedAmount` 150 succeeds, stored `ChangeAmount` is 50, journal is balanced, `ExpectedCash` rises by 100.
- [ ] Two card lines 60 + 60 on a 100 order returns 400.
- [ ] Exact payment and allowed partial payment behave as before.

## Tests required
Integration tests in `PosOrderTests`, one per criterion; assert journal debit equals credit (reuse `PosLedgerTests` helpers) and session `ExpectedCash`.

## Do not touch
Void flow, price override rules, ledger account roles.

## What I did (Antigravity)

## Review (Claude)
