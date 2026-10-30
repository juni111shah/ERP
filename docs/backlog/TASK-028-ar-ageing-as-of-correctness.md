---
id: TASK-028
title: AR ageing and customer statement must reflect the as-of date
priority: P1
status: done
schema: no
depends_on: [TASK-025]
area: finance-reports
audit_ref: Finding recorded on TASK-025
commit: 2b4910c
---

## Problem
`ArReportHandlers.OpenAsync` and `UnallocatedAsync` (`ERP.Application/Finance/ArAgeing.cs`) used the invoice's current `Paid`, current status and today's `Unallocated`, so a re-run for a past date did not reproduce it (same flaw as AP, fixed in TASK-025).

## Change wanted
Compute outstanding as of the date: receipt allocations and credit applications dated on or before the as-of date, receipts not voided by then, invoices voided after the date still shown.

## Acceptance criteria
- [x] Invoice paid on 15 Feb is open in the 31 Jan report and settled on 28 Feb.
- [x] Invoice voided later still appears in the earlier report.
- [x] A receipt dated after the date is not counted as on account.
- [x] Ageing as of today is unchanged.

## What I did (Claude, on user request)
Rewrote `OpenAsync` and `UnallocatedAsync` as-of aware: allocations count from their receipt date, credit applications by their own date, decimal sums done client-side for SQLite. Test: `Ageing_and_statement_reproduce_a_past_as_of_date` in `ReceivablesTests`.

## Review (Claude)
- dotnet build 0 errors; 238 unit and 242 integration tests pass, 4 skipped (SQL Server).
