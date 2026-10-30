# Board

Last Claude run: 2026-10-30 (reviewed TASK-126..130, all done: clean-copy build 0 errors, unit 286 and integration 364 passed, 8 SQL Server tests skipped; wrote TASK-131..138 from Procure to pay second pass).
Rotation position: second pass, Procure to pay done (Purchasing module and payables). Next run analyses POS and Retail.
Needs human: Decide whether to merge antigravity/dashboard-and-modules into main (never pushed). Schema change TASK-010 (script database/scripts/0026_IdempotencyRecords.sql) must be applied by a human. TASK-021 criterion 1 (20 parallel receipts) is unproven until TASK-022 lands. TASK-006 commit message lacks the `TASK-006:` prefix. C: disk is 94% full (21G free). TASK-009 commit message lacked the `TASK-009:` prefix (hash on card was amended away; reviewed 8787997).  Migration scripts 0026 (Idempotency), 0027 (POS one open session per register; fails if a register has two open sessions) and 0065 (finance reporting indexes; locks tables while building) are not applied to any database. Branch antigravity/dashboard-and-modules was pushed; main rejects direct pushes (repository rules), so merge via pull request. TASK-128 SQL Server race tests are skipped locally (unproven). Migration script 0066 (InvoiceLineSourceLine) not applied. Commit 710861a bundles TASK-126..129. Known gaps: CreateStockCount numbering races (CountAsync+1); about 56 selects/date pickers lack aria-labels and many forms lack validation messages; Services module not analysed; SQL Server parallel tests unrun locally.

| Status | Count |
|---|---|
| open | 8 |
| in-progress | 0 |
| ready-for-review | 0 |
| changes-requested | 0 |
| blocked | 0 |
| done | 82 |

| Card | Priority | Status | Title |
|---|---|---|---|
| TASK-001 | P0 | done | Authorize attachments by the owning record's permission |
| TASK-002 | P0 | done | Block portal tokens from staff endpoints with a fallback policy |
| TASK-003 | P1 | done | Replace blanket InvalidOperation/KeyNotFound mapping with domain exceptions |
| TASK-004 | P0 | done | Make work-order release/complete safe and post correct inventory journals |
| TASK-005 | P0 | done | POS orders priced and validated on the server |
| TASK-006 | P0 | done | POS void reverses cash, stock at original cost, and respects closed sessions |
| TASK-007 | P0 | done | Post POS sales and tax to the ledger |
| TASK-008 | P0 | done | Goods receipts post to a GR/IR clearing account and the bill clears it |
| TASK-009 | P1 | done | Enforce allowed status transitions on all module documents |
| TASK-010 | P0 | done | Make documents and their postings one transaction; add idempotency keys |
| TASK-011 | P0 | done | Project and service billing must create invoices or fail, using customer terms |
| TASK-012 | P1 | done | Remove hardcoded currency, tax rate and placeholder GUIDs from module pages |
| TASK-013 | P1 | done | Gate create, edit, delete and action buttons by permission in the nine new modules |
| TASK-014 | P0 | done | Upgrade Angular to patched 19.x and add security gates to CI |
| TASK-015 | P1 | done | Add integration tests for Sales and Purchasing document flows |
| TASK-016 | P0 | done | Invoice generated from a sales order keeps tax, source link and currency |
| TASK-017 | P0 | done | Sales order edit, fulfilment and invoice due-date guards |
| TASK-018 | P1 | done | Reopen the sales order when its generated invoice is voided |
| TASK-019 | P1 | done | Receipts, payments, cheques and transfers must use accounts flagged as bank or cash |
| TASK-020 | P1 | done | Reject or round money amounts to two decimals in receipts, payments, cheques, bank and journals |
| TASK-021 | P2 | done | Retry document commands when a number or allocation hits a concurrency conflict |
| TASK-022 | P1 | done | Run integration tests on SQL Server (or per-request SQLite connections) so concurrency can be tested |
| TASK-023 | P1 | done | Make bill posting, payment allocation/void and cheque clearing retryable and atomic |
| TASK-024 | P1 | done | Bills and payments accept only vendor parties |
| TASK-025 | P1 | done | AP ageing and vendor statement must reflect the as-of date |
| TASK-026 | P2 | done | Validate allocate-payment and issued-cheque clearing requests |
| TASK-027 | P1 | done | Editing or deleting a bill withdraws its pending approval request |
| TASK-028 | P0 | done | POS rejects or caps overpayment so the sale journal always balances |
| TASK-029 | P0 | done | Session drawer and close use atomic updates and the caller's identity |
| TASK-030 | P1 | done | Enforce one open session per register in the database |
| TASK-031 | P1 | done | Sales use the register's warehouse and cash account; inactive registers cannot trade |
| TASK-032 | P1 | done | Void needs a reason; free-text lines need price override permission |
| TASK-033 | P2 | done | POS KPIs aggregate in SQL, respect the legal entity, drop hardcoded AED |
| TASK-034 | P1 | done | AR ageing and customer statement must reflect the as-of date |
| TASK-040 | P1 | done | Backdated stock issues, reductions and transfers must not drive a later balance negative |
| TASK-041 | P2 | done | Validate dates, ids and precision on stock receive, issue, adjust and transfer |
| TASK-042 | P2 | done | A warehouse that still holds stock cannot be deactivated |
| TASK-043 | P1 | done | Validate BOM components, items and duplicate codes |
| TASK-044 | P1 | done | Work orders need an active BOM in the same entity and a valid warehouse; a BOM in use cannot be deleted |
| TASK-050 | P1 | done | Tasks stay in their project and follow the status flow |
| TASK-051 | P1 | done | Timesheets validate task, project state and money precision |
| TASK-052 | P1 | done | Project status, customer role and delete are guarded |
| TASK-053 | P1 | done | Milestones: editable when pending, immutable when invoiced, sane dates |
| TASK-054 | P1 | done | Billing honours the project billing type and status |
| TASK-060 | P1 | done | CRM records check legal entity, customer and related record; interactions record the caller |
| TASK-061 | P0 | done | Lead conversion works, is atomic and happens once; converted leads are frozen |
| TASK-062 | P1 | done | Opportunity close follows the pipeline; closed stages force probability; reopening clears the close |
| TASK-063 | P1 | done | Van sales trips need a real route and catalog items with sensible quantities |
| TASK-064 | P1 | done | Trip completion validates the reconciliation against what was loaded |
| TASK-070 | P2 | done | Ledger reports validate their date ranges |
| TASK-071 | P1 | done | Manual journals may only use active, postable accounts |
| TASK-072 | P2 | done | Bank transfers and imported statement lines need a real date |
| TASK-073 | P1 | done | A statement line already booked cannot be booked a second time |
| TASK-080 | P1 | done | Pay amounts are money and an adjustment cannot make pay negative |
| TASK-081 | P1 | done | Paying payroll validates the date and payroll commands retry on conflicts |
| TASK-082 | P1 | done | Changing or deleting a payroll withdraws its pending approval |
| TASK-083 | P2 | done | Attendance cannot change after payroll is posted or for a month the employee was not employed |
| TASK-084 | P2 | done | Employee IBAN must have a valid shape and is stored upper case |
| TASK-090 | P0 | done | Invoice the pro-rated instalment of a terminated, ended or renewed lease |
| TASK-091 | P1 | done | Lease needs a customer tenant, an existing tax code and an available unit |
| TASK-092 | P1 | done | Lease creation with activation and lease renewal are atomic and retried |
| TASK-093 | P1 | done | Security deposit commands use bank accounts and cent-precise amounts |
| TASK-094 | P2 | done | An inspection's lease must belong to the chosen unit |
| TASK-100 | P1 | done | Failed password or 2FA confirmations count toward lockout and are rate limited |
| TASK-101 | P1 | done | Password change, reset and user deactivation invalidate existing access tokens |
| TASK-102 | P2 | done | Throttle portal sign-in code requests per customer or vendor |
| TASK-103 | P2 | done | Guard test: every API endpoint declares authorization |
| TASK-110 | P2 | done | Icon-only buttons need an accessible name |
| TASK-111 | P1 | done | Clickable cards, chips and rows must work from the keyboard |
| TASK-112 | P2 | done | Inputs labelled only by a placeholder need an accessible name |
| TASK-113 | P1 | done | The shared table shows a load failure with a retry, not "Nothing here yet" |
| TASK-120 | P2 | done | Dashboard status charts use one grouped query instead of one COUNT per status |
| TASK-121 | P2 | done | Project billing overview reads only needed columns and groups once |
| TASK-122 | P2 | done | Member list builds roles with lookups; history and role changes load only the users they need |
| TASK-123 | P2 | done | Add entity/status/date indexes for journal entries, invoices, bills and settlements |
| TASK-124 | P2 | done | Stock count save/post and work-order completion stop querying once per line |
| TASK-125 | P2 | done | Compress JSON and the served SPA with Brotli/gzip |
|  |  | done | Invoices and receipts accept only customer parties |
|  |  | done | The public invoice API must not accept SourceType and SourceId |
|  |  | done | Allocate receipt, void receipt, void invoice and update invoice retry on concurrency conflicts |
|  |  | done | Validate dates and quantity/price precision on invoice and receipt inputs |
|  |  | done | Cumulative credited quantity per invoice line cannot exceed the invoiced quantity (schema: yes) |
| TASK-131 | P1 | open | Goods receipts accepted only for open purchase orders; item data from the order line |
| TASK-132 | P1 | open | Goods receipt lines checked together, retry on conflicts, date and precision rules |
| TASK-133 | P1 | open | Purchase orders editable only while Draft/Submitted; delete guards |
| TASK-134 | P1 | open | PO to bill keeps tax and uses a valid due date |
| TASK-135 | P1 | open | PO to bill atomic, configured accounts, validated overrides |
| TASK-136 | P2 | open | Purchase order vendor, currency, dates and precision validated |
| TASK-137 | P2 | open | Bill input dates, precision, active tax codes |
| TASK-138 | P2 | open | Payment and cheque dates validated; bill edit/delete retry |
| TASK-139 | P1 | open | Demo data covers every status of every document type across three years |
