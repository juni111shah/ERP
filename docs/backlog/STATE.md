# Board

Last Claude run: 2026-10-30 (no cards ready-for-review; queue was empty so analysed Procure to pay and wrote TASK-023 to 027)
Rotation position: Procure to pay analysed (bills, payments, AP ageing). Next area = POS and Retail
Needs human: Decide whether to merge antigravity/dashboard-and-modules into main (never pushed). Schema change TASK-010 (script database/scripts/0021_IdempotencyRecords.sql) must be applied by a human. TASK-021 criterion 1 (20 parallel receipts) is unproven until TASK-022 lands. TASK-006 commit message lacks the `TASK-006:` prefix. C: disk is 94% full (21G free). TASK-009 commit message lacked the `TASK-009:` prefix (hash on card was amended away; reviewed 8787997). TASK-014 blocked: Angular 19.x has no patch for the advisories; needs a human decision on a major upgrade to v20+.

| Status | Count |
|---|---|
| open | 5 |
| in-progress | 0 |
| ready-for-review | 0 |
| changes-requested | 0 |
| blocked | 1 |
| done | 21 |

| Card | Priority | Status | Title |
|---|---|---|---|
| TASK-001 | P0 | done | Authorize attachments by the owning record's permission |
| TASK-002 | P0 | done | Block portal tokens from staff endpoints |
| TASK-003 | P1 | done | Replace blanket exception mapping with domain exceptions |
| TASK-004 | P0 | done | Work-order release/complete safe, correct inventory journals |
| TASK-005 | P0 | done | POS orders priced and validated on the server |
| TASK-006 | P0 | done | POS void reverses cash, stock at original cost |
| TASK-007 | P0 | done | Post POS sales and tax to the ledger |
| TASK-008 | P0 | done | GR/IR clearing and bill matching |
| TASK-009 | P1 | done | Enforce status transitions on module documents |
| TASK-010 | P0 | done | Transactional postings and idempotency keys (schema) |
| TASK-011 | P0 | done | Project and service billing create invoices or fail |
| TASK-012 | P1 | done | Remove hardcoded currency, tax, placeholder GUIDs |
| TASK-013 | P1 | done | UI permission gating in new modules |
| TASK-014 | P0 | blocked | Angular security upgrade and CI gates |
| TASK-015 | P1 | done | Integration tests for Sales and Purchasing |
| TASK-016 | P0 | done | Invoice from sales order keeps tax, source link, currency |
| TASK-017 | P0 | done | Sales order edit, fulfilment and due-date guards |
| TASK-018 | P1 | done | Reopen sales order when its invoice is voided |
| TASK-019 | P1 | done | Bank/cash account must be flagged IsBank (receipts, payments, cheques, transfers) |
| TASK-020 | P1 | done | Reject or round money amounts to two decimals |
| TASK-021 | P2 | done | Retry document commands on number/allocation concurrency conflicts |
| TASK-022 | P1 | done | Concurrency-capable integration test host (SQL Server) |
| TASK-023 | P1 | open | Retry and atomicity for bill post, payment allocate/void, cheque clear |
| TASK-024 | P1 | open | Bills and payments accept only vendor parties |
| TASK-025 | P1 | open | AP ageing and vendor statement honour the as-of date |
| TASK-026 | P2 | open | Validate allocate-payment and issued-cheque clearing |
| TASK-027 | P1 | open | Editing or deleting a bill withdraws its pending approval |
