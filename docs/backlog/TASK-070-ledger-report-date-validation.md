---
id: TASK-070
title: Ledger reports validate their date ranges
priority: P2
status: done
schema: no
depends_on: []
area: finance-reports
audit_ref: Claude analysis, Finance reports and period close
commit: 4b96a7f
---

## Problem
GetTrialBalance, GetGeneralLedger, GetIncomeStatement and GetBalanceSheet (`LedgerReports.cs`) have no validators, unlike the party ledgers (`PartyLedger.cs`: end >= start, 10-year cap). `from > to`, `0001-01-01` dates and a 20-year range are accepted; the handlers load every journal line up to `to` into memory.

## Impact
Silent empty or nonsensical reports for a reversed range; an unbounded range loads the whole ledger.

## Change wanted
Add validators (shared `ReportRange` helper): `to` and `from` not default, `from <= to`, range <= `PartyLedger.MaxDays` when `from` is given; balance sheet `asOf` not default. An open-ended trial balance (no `from`) stays allowed.

## Acceptance criteria
- [x] Reversed range, default date and >10-year range return 400 on trial balance, general ledger, income statement and balance sheet.
- [x] Valid ranges and the open-ended trial balance still return 200.

## Tests required
`LedgerTests.Ledger_reports_reject_reversed_or_oversized_date_ranges`.

## Do not touch
Report DTOs and endpoints.

## What I did (Claude, parallel agent)
Changed LedgerReports.cs, LedgerTests.cs. dotnet build 0 errors; 253 unit + 281 integration tests pass, 4 skipped (SQL Server).

## Review (Claude)
Analysis note: period-lock changes need no new audit event, the generic audit interceptor already records LegalEntity updates including `BooksLockedTo`.
