---
id: TASK-123
title: Add entity/status/date indexes for journal entries, invoices, bills and settlements
priority: P2
status: done
schema: yes
depends_on: []
area: performance
audit_ref: Claude analysis, Performance
commit:
---

## Problem
Trial balance, ageing, statements, dashboards and books-lock checks filter `JournalEntries`, `Invoices`, `Bills` and `Settlements` by workspace, legal entity, status and a date range, but the only composite indexes are the unique `Number` ones (`AppDbContext.cs` entity configuration); date and status filters scan.

## Impact
Report and dashboard latency grows linearly with document volume.

## Change wanted
Add non-unique indexes `(OrgId, LegalEntityId, Status, Date)` on JournalEntries, `(OrgId, LegalEntityId, Status, InvoiceDate)` on Invoices, `(OrgId, LegalEntityId, Status, BillDate)` on Bills, `(OrgId, LegalEntityId, Direction, Date)` on Settlements. Migration `FinanceReportingIndexes` and script `database/scripts/0065_FinanceReportingIndexes.sql`.

## Acceptance criteria
- [ ] Model has the four indexes; `has-pending-model-changes` reports none.
- [ ] Script contains the four CREATE INDEX statements and the history row.

## Tests required
`ReportingIndexTests` asserts the model indexes.

## Do not touch
Existing indexes, queries.

## What I did (Claude, on user request)
Added the indexes, generated the migration and script. Not applied to any database; building the indexes on a large production table takes a lock, schedule it off-hours.

## Review (Claude)
