---
id: TASK-072
title: Bank transfers and imported statement lines need a real date
priority: P2
status: done
schema: no
depends_on: []
area: finance
audit_ref: Claude analysis, Finance reports and period close
commit: HASH
---

## Problem
`TransferFundsValidator` and `ImportStatementValidator` (`BankFeatures.cs`) accept `0001-01-01` dates (a transfer posts a journal dated 0001; an imported line dated 0001 can never match). `TransferFunds.Memo` has no length limit.

## Impact
Journals dated year 1 and unmatchable statement lines.

## Change wanted
Transfer: `Date` not default, `Memo` max 300. Import: each line `Date` not default.

## Acceptance criteria
- [x] A transfer or imported line dated 0001-01-01 returns 400.
- [x] Existing bank tests pass.

## Tests required
`BankTests.Statement_lines_and_transfers_need_a_real_date_and_a_booked_line_cannot_be_booked_twice` (first half).

## Do not touch
Matching logic.

## What I did (Claude, parallel agent)
Changed BankFeatures.cs, BankTests.cs. dotnet build 0 errors; 253 unit + 281 integration tests pass, 4 skipped (SQL Server).

## Review (Claude)
Analysis note: period-lock changes need no new audit event, the generic audit interceptor already records LegalEntity updates including `BooksLockedTo`.
