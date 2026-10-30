---
id: TASK-093
title: Security deposit commands use bank accounts and cent-precise amounts
priority: P1
status: done
schema: no
depends_on: []
area: realestate
audit_ref: Claude analysis, Real estate
commit: HASH093
---

## Problem
`DepositHandlers.RequireBankAsync` (`FinanceFeatures.cs`) accepts any postable active Asset account, so receivables or inventory can be chosen as the deposit bank or refund account (the rule TASK-019 introduced is `BankAccountRules.IsCashOrBank`). The validators only check `> 0`/`>= 0` with no `MustBeMoney()` (TASK-020) and no check of the date.

## Impact
Deposit cash can be booked to a non-bank asset account, amounts with sub-cent digits are posted and later rounded, and a default date posts a journal in year 0001.

## Change wanted
Use `BankAccountRules.IsCashOrBank` in `RequireBankAsync`; add `MustBeMoney()` to Amount, Damages, Refund and arrears amounts; require a non-default date on record and settle.

## Acceptance criteria
- [x] A receivables account as the bank returns 400.
- [x] An amount with more than two decimals returns 400.
- [x] A default date returns 400.
- [x] Existing deposit tests pass.

## Tests required
`RealEstateBacklogTests.Deposit_money_goes_through_a_bank_account_and_to_the_cent`.

## Do not touch
Deposit accounting, settlement rules.

## What I did (Claude)
Implemented as described; see the commit.

## Review (Claude)
Self-implemented on user request. `dotnet build backend/ERP.sln` 0 errors; `dotnet test backend/ERP.sln` passes (SQL Server tests skipped locally).
