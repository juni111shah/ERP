# Board

Last Claude run: 2026-10-25 (reviewed TASK-004 -> done; 11 open, no refill)
Rotation position: next area = Order to cash (Sales, invoices, receipts)
Needs human: DISK FULL on C: (ENOSPC) at the 2026-10-25 15:3x run. The clean-copy build of TASK-008 (f46af53) failed with "not enough space on the disk", so TASK-008 is NOT reviewed and stays ready-for-review. Free space on C: (scratchpad build copies under %TEMP%\claude, dotnet/npm caches), then re-run the review. Review notes so far: diff reads correct (clearing debited for received qty, variance line, tolerance check, billed-PO guard, reversal in transaction); card `commit:` says 84b8486 but the real commit is f46af53. TASK-014 is blocked legitimately (Angular 19.x has no patch; needs a human decision on a major upgrade to v20+). No refill done (11 open).

| Status | Count |
|---|---|
| open | 11 |
| in-progress | 1 |
| ready-for-review | 0 |
| changes-requested | 0 |
| blocked | 0 |
| done | 3 |

| Card | Priority | Status | Title |
|---|---|---|---|
| TASK-001 | P0 | done | Authorize attachments by the owning record's permission |
| TASK-002 | P0 | done | Block portal tokens from staff endpoints |
| TASK-003 | P1 | open | Replace blanket exception mapping with domain exceptions |
| TASK-004 | P0 | done | Work-order release/complete safe, correct inventory journals |
| TASK-005 | P0 | open | POS orders priced and validated on the server |
| TASK-006 | P0 | open | POS void reverses cash, stock at original cost |
| TASK-007 | P0 | open | Post POS sales and tax to the ledger |
| TASK-008 | P0 | in-progress | GR/IR clearing and bill matching |
| TASK-009 | P1 | open | Enforce status transitions on module documents |
| TASK-010 | P0 | open | Transactional postings and idempotency keys (schema) |
| TASK-011 | P0 | open | Project and service billing create invoices or fail |
| TASK-012 | P1 | open | Remove hardcoded currency, tax, placeholder GUIDs |
| TASK-013 | P1 | open | UI permission gating in new modules |
| TASK-014 | P0 | open | Angular security upgrade and CI gates |
| TASK-015 | P1 | open | Integration tests for Sales and Purchasing |
