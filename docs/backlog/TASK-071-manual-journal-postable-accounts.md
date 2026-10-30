---
id: TASK-071
title: Manual journals may only use active, postable accounts
priority: P1
status: done
schema: no
depends_on: []
area: finance
audit_ref: Claude analysis, Finance reports and period close
commit: HASH
---

## Problem
`JournalHandlers.ApplyAsync` (`JournalFeatures.cs`) only checks that the accounts exist. `PostJournal` calls `journal.Post` directly and never goes through `PostingService.EnsureAccountsAsync`, so header (non-postable) and inactive accounts can be journalled and posted by hand, while every document posting rejects them.

## Impact
Balances land on header accounts (double counting in tree roll-ups) and on deactivated accounts.

## Change wanted
Add `EnsurePostableAsync` (found, `IsActive`, `IsPostable`; `FieldValidationException("lines", ...)`) and call it in `ApplyAsync` (create/update) and in `PostJournal` (the account may have been deactivated after drafting).

## Acceptance criteria
- [x] Creating a journal on a header account returns 400.
- [x] Posting a draft whose account was deactivated afterwards returns 400 and the journal stays a draft.
- [x] A journal on active postable accounts still posts.

## Tests required
`LedgerTests.A_manual_journal_cannot_use_a_header_or_inactive_account_even_if_deactivated_after_drafting`.

## Do not touch
Document postings.

## What I did (Claude, parallel agent)
Changed JournalFeatures.cs, LedgerTests.cs. dotnet build 0 errors; 253 unit + 281 integration tests pass, 4 skipped (SQL Server).

## Review (Claude)
Analysis note: period-lock changes need no new audit event, the generic audit interceptor already records LegalEntity updates including `BooksLockedTo`.
