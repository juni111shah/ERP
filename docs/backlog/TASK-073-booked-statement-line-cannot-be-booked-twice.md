---
id: TASK-073
title: A statement line already booked cannot be booked a second time
priority: P1
status: done
schema: no
depends_on: []
area: finance
audit_ref: Claude analysis, Finance reports and period close
commit: HASH
---

## Problem
`BookStatementLine` (`BankFeatures.cs`) only rejects lines with status Matched. `UnmatchStatementLine` sets a booked line back to Unmatched while its journal (SourceType `bank`, SourceId = line id) stays posted; booking the line again posts a second journal for the same bank movement.

## Impact
The bank movement is posted twice; the ledger balance no longer reconciles to the statement.

## Change wanted
In `BookStatementLine`, throw `ConflictException` when a posted journal with SourceType `bank` and SourceId = line id exists ("match it to its ledger entry instead"). Unmatch and manual rematch stay allowed (existing behaviour).

## Acceptance criteria
- [x] Book, unmatch, book again returns 409 and posts no second journal.
- [x] Book, unmatch, manual match still works (existing test).

## Tests required
`BankTests.Statement_lines_and_transfers_need_a_real_date_and_a_booked_line_cannot_be_booked_twice` (second half).

## Do not touch
Unmatch behaviour.

## What I did (Claude, parallel agent)
Changed BankFeatures.cs, BankTests.cs. dotnet build 0 errors; 253 unit + 281 integration tests pass, 4 skipped (SQL Server).

## Review (Claude)
Analysis note: period-lock changes need no new audit event, the generic audit interceptor already records LegalEntity updates including `BooksLockedTo`.
