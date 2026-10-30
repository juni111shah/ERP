---
id: TASK-139
title: Demo data covers every status of every document type across three years
priority: P1
status: open
schema: no
depends_on: []
area: platform
audit_ref: data coverage audit of ERP_Dev, 2026-10-30
commit:
---

## Problem
A read-only audit of ERP_Dev (today 2026-10-30, window 2023-11 to 2026-10) found that only invoices, bills, settlements and journals run over three years. Everything else is 2026 only, and many statuses have no rows:
- Sales orders: Draft, Processing, Invoiced, Cancelled missing. Quotes: Draft, Declined, Expired, Converted missing. Purchase orders: Draft, Submitted, PartiallyReceived, Received, Billed, Cancelled missing. Requisitions: Draft, PendingApproval, Rejected, Ordered missing.
- Invoices: one credit note, one draft, no voided invoice. Bills: only Paid and 4 Issued, no Draft, PartlyPaid or Voided. Journals: only Posted (no Draft, no Reversed). Cheques: nothing before 2026; Bounced, Replaced, Cancelled and Issued missing.
- POS orders: no Voided or Parked. Work orders: Draft and Cancelled missing. Projects, engagements, retainers, leases, payroll, CRM, van sales: 2026 only, one or two rows each, several statuses missing. Leave requests, stock counts, approval requests, timesheets and project milestones are empty.
- 7 invoices and 7 journals are dated after today (up to 2026-12-15).
- Fixed separately (commit 61e4493 plus a repair script): the 51 `JV-BASE-nnn-INV` journals were unbalanced by the invoice tax.

## Impact
Reports, ageing, dashboards, status filters and year-on-year comparisons cannot be demonstrated or tested, and status-specific screens and transitions have no data.

## Change wanted
- Extend the demo seeder (`backend/src/ERP.Api/ProgramSeeder.cs`) so each document type has rows for every status of its enum, with activity spread over the 36 months to 2026-10: sales (quotes, orders, invoices including credit notes and voids), purchasing (requisitions, orders, receipts, bills including partly paid and voided), cheques (all instrument statuses), journals (draft, posted, reversed), POS (completed, voided, parked), manufacturing, projects (all statuses, milestones, timesheets), services, HR (leave and payroll in all statuses), real estate (leases in all statuses), CRM (all lead and opportunity stages), stock counts, approval requests.
- Documents are dated up to the seed date only, never in the future, and every ledger entry balances.
- Keep the seed deterministic and idempotent, with a reasonable run time.

## Acceptance criteria
- [ ] A query per document type shows every enum status with at least one row in the seeded database.
- [ ] Each of the three years has rows for sales, purchasing, finance, POS and HR documents.
- [ ] No document is dated after the seed date; all journals balance.
- [ ] A test seeds a fresh database and asserts the above (status coverage by enum, balanced journals).

## Tests required
An integration test over a freshly seeded database; unit tests for any new helper.

## Do not touch
Production behaviour of the posting services; seed data of other demo organisations.

## What I did (Antigravity)

## Review (Claude)
