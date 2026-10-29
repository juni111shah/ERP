# Board

Last Claude run: 2026-10-30 04:15 (TASK-012 reviewed done: clean-copy build 0 errors, 223+201 tests pass, ng build ok; TASK-006 in-progress ~30 min, no stall; refilled with TASK-016..018 from Order to cash)
Rotation position: Order to cash analysed (Sales order to invoice). Next area = Procure to pay (Purchasing, bills, payments)
Needs human: C: disk is 94% full (21G free). TASK-009 commit message lacked the `TASK-009:` prefix (hash on card was amended away; reviewed 8787997). TASK-014 blocked: Angular 19.x has no patch for the advisories; needs a human decision on a major upgrade to v20+.

| Status | Count |
|---|---|
| open | 7 |
| in-progress | 1 |
| ready-for-review | 0 |
| changes-requested | 0 |
| blocked | 1 |
| done | 9 |

| Card | Priority | Status | Title |
|---|---|---|---|
| TASK-001 | P0 | done | Authorize attachments by the owning record's permission |
| TASK-002 | P0 | done | Block portal tokens from staff endpoints |
| TASK-003 | P1 | done | Replace blanket exception mapping with domain exceptions |
| TASK-004 | P0 | done | Work-order release/complete safe, correct inventory journals |
| TASK-005 | P0 | done | POS orders priced and validated on the server |
| TASK-006 | P0 | in-progress | POS void reverses cash, stock at original cost |
| TASK-007 | P0 | open | Post POS sales and tax to the ledger |
| TASK-008 | P0 | done | GR/IR clearing and bill matching |
| TASK-009 | P1 | done | Enforce status transitions on module documents |
| TASK-010 | P0 | open | Transactional postings and idempotency keys (schema) |
| TASK-011 | P0 | done | Project and service billing create invoices or fail |
| TASK-012 | P1 | done | Remove hardcoded currency, tax, placeholder GUIDs |
| TASK-013 | P1 | open | UI permission gating in new modules |
| TASK-014 | P0 | blocked | Angular security upgrade and CI gates |
| TASK-015 | P1 | open | Integration tests for Sales and Purchasing |
| TASK-016 | P0 | open | Invoice from sales order keeps tax, source link, currency |
| TASK-017 | P0 | open | Sales order edit, fulfilment and due-date guards |
| TASK-018 | P1 | open | Reopen sales order when its invoice is voided |
