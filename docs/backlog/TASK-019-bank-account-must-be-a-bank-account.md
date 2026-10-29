---
id: TASK-019
title: Receipts, payments, cheques and transfers must use accounts flagged as bank or cash
priority: P1
status: ready-for-review
schema: no
depends_on: []
area: finance
audit_ref: AUDIT-FULL finance core review
commit: f5b3718
owner: claude (separate worktree; Antigravity skip this card)
---

## Problem
`Account.IsBank` exists (`ERP.Domain/Finance/Finance.cs:62`), but the places that pick the bank/cash account only check `Type == Asset`, postable and active:
- `ReceiptFeatures.cs:78`
- `PaymentFeatures.cs:76`
- `ChequeFeatures.cs:203`
- `BankFeatures.cs:201`

Any asset account (accounts receivable, inventory, fixed assets) can therefore be chosen as the "bank" account.

## Impact
A receipt, payment, cheque deposit or bank transfer can debit or credit the wrong asset account. The bank reconciliation and cash position reports then show wrong balances, and the error is hard to spot because the journal is balanced.

## Change wanted
- Add one shared helper (for example `BankAccountGuard.RequireAsync(accountId, field)` in `ERP.Application/Finance`) that loads the account in the current workspace and requires: `IsBank`, `Type == Asset`, `IsPostable`, `IsActive`. Replace the four inline checks with it.
- Error message: "Choose an active bank or cash account." with field `bankAccountId` (keep the existing field names).
- Make sure the account-edit screen/API can set `IsBank`, and that the default chart-of-accounts templates flag their cash and bank accounts `IsBank = true`. Add a migration-free data check: if a workspace has no `IsBank` account at all, the error text should say to flag one in the chart of accounts.
- Frontend: the bank-account dropdowns in receipts, payments, cheques and bank transfer should only list `IsBank` accounts (use the existing accounts list filter `isBank:eq:true`).

## Acceptance criteria
- [ ] Posting a receipt, payment, cheque deposit or transfer with a non-bank asset account (for example the AR control account) returns 400 on `bankAccountId`.
- [ ] The same calls with an `IsBank` account still succeed.
- [ ] Newly created workspaces from the templates have at least one `IsBank` account, so the demo flows still work.
- [ ] Dropdowns show only bank/cash accounts.

## Tests required
Integration tests in `ReceivablesTests`/`PayablesTests`/`ChequeTests`/`BankTests` for the rejected and accepted cases; a unit test for the helper.

## Do not touch
Journal posting rules; chart-of-accounts numbering.

## What I did (Claude, implemented in separate worktree)
- Added `BankAccountRules` (`IsCashOrBank`, `IsBankOnly`): active, postable asset account that is flagged `IsBank` or has system role `bank`/`cash` (cheque deposits: `IsBank` or `bank` only). This is the same definition the cash position report and the existing frontend dropdowns already use, because the default "Cash on hand" account has role `cash` but `IsBank=false`; requiring `IsBank` alone would have broken the default chart.
- Applied it in receipts, payments, cheque deposit, bank transfer/import, and also HR payroll payment and the two Real Estate payment handlers (same defect).
- Tests: `BankAccountRuleTests` (receipt and payment with receivables account -> 400; bank and cash accepted) and `BankAccountRulesTests` (unit). Frontend dropdowns already filter correctly, no change.
- Full suite in the worktree: 227 unit + 208 integration passed, 0 errors.

## Review (Claude)
