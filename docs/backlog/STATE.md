# Board

Last Claude run: 2026-10-30 (tenth run: no cards ready-for-review, no in-progress cards, 0 open so refilled with TASK-028 to 033)
Rotation position: POS and Retail analysed (orders, sessions, registers, KPIs). Next area = Inventory and Manufacturing
Needs human: Decide whether to merge antigravity/dashboard-and-modules into main (never pushed). Schema change TASK-010 (script database/scripts/0026_IdempotencyRecords.sql) must be applied by a human. TASK-021 criterion 1 (20 parallel receipts) is unproven until TASK-022 lands. TASK-006 commit message lacks the `TASK-006:` prefix. C: disk is 94% full (21G free). TASK-009 commit message lacked the `TASK-009:` prefix (hash on card was amended away; reviewed 8787997). 

| Status | Count |
|---|---|
| open | 4 |
| in-progress | 0 |
| ready-for-review | 0 |
| changes-requested | 0 |
| blocked | 0 |
| done | 30 |

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
| TASK-014 | P0 | done | Angular security upgrade and CI gates |
| TASK-015 | P1 | done | Integration tests for Sales and Purchasing |
| TASK-016 | P0 | done | Invoice from sales order keeps tax, source link, currency |
| TASK-017 | P0 | done | Sales order edit, fulfilment and due-date guards |
| TASK-018 | P1 | done | Reopen sales order when its invoice is voided |
| TASK-019 | P1 | done | Bank/cash account must be flagged IsBank (receipts, payments, cheques, transfers) |
| TASK-020 | P1 | done | Reject or round money amounts to two decimals |
| TASK-021 | P2 | done | Retry document commands on number/allocation concurrency conflicts |
| TASK-022 | P1 | done | Concurrency-capable integration test host (SQL Server) |
| TASK-023 | P1 | done | Retry and atomicity for bill post, payment allocate/void, cheque clear |
| TASK-024 | P1 | done | Bills and payments accept only vendor parties |
| TASK-025 | P1 | done | AP ageing and vendor statement honour the as-of date |
| TASK-026 | P2 | done | Validate allocate-payment and issued-cheque clearing |
| TASK-027 | P1 | done | Editing or deleting a bill withdraws its pending approval |
| TASK-028 | P0 | done | POS rejects or caps overpayment so the sale journal always balances |
| TASK-029 | P0 | open | Session drawer and close use atomic updates and the caller's identity |
| TASK-030 | P1 | open | Enforce one open session per register in the database (schema) |
| TASK-031 | P1 | open | Sales use the register's warehouse and cash account; inactive registers cannot trade |
| TASK-032 | P1 | done | Void needs a reason; free-text lines need price override permission |
| TASK-033 | P2 | open | POS KPIs aggregate in SQL, respect the legal entity, drop hardcoded AED |
| TASK-034 | P1 | done | AR ageing and customer statement honour the as-of date |
