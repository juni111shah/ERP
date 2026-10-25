# Board

Last Claude run: 2026-10-25 (reviewed TASK-003 -> done; 8 open, no refill)
Rotation position: next area = Order to cash (Sales, invoices, receipts)
Needs human: C: disk is 100% full (reviews built from D:RP_review_tmp; free C: space). TASK-014 blocked: Angular 19.x has no patch for the advisories; needs a human decision on a major upgrade to v20+.

| Status | Count |
|---|---|
| open | 8 |
| in-progress | 1 |
| ready-for-review | 0 |
| changes-requested | 0 |
| blocked | 1 |
| done | 5 |

| Card | Priority | Status | Title |
|---|---|---|---|
| TASK-001 | P0 | done | Authorize attachments by the owning record's permission |
| TASK-002 | P0 | done | Block portal tokens from staff endpoints |
| TASK-003 | P1 | done | Replace blanket exception mapping with domain exceptions |
| TASK-004 | P0 | done | Work-order release/complete safe, correct inventory journals |
| TASK-005 | P0 | in-progress | POS orders priced and validated on the server |
| TASK-006 | P0 | open | POS void reverses cash, stock at original cost |
| TASK-007 | P0 | open | Post POS sales and tax to the ledger |
| TASK-008 | P0 | done | GR/IR clearing and bill matching |
| TASK-009 | P1 | open | Enforce status transitions on module documents |
| TASK-010 | P0 | open | Transactional postings and idempotency keys (schema) |
| TASK-011 | P0 | open | Project and service billing create invoices or fail |
| TASK-012 | P1 | open | Remove hardcoded currency, tax, placeholder GUIDs |
| TASK-013 | P1 | open | UI permission gating in new modules |
| TASK-014 | P0 | blocked | Angular security upgrade and CI gates |
| TASK-015 | P1 | open | Integration tests for Sales and Purchasing |
