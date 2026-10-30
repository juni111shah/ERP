---
id: TASK-135
title: Converting a purchase order to a bill is atomic, uses configured accounts and validates price overrides
priority: P1
status: open
schema: no
depends_on: []
area: purchasing
audit_ref: Claude analysis, Procure to pay (second pass)
commit:
---

## Problem
`ConvertPurchaseOrderToBill` (backend/modules/ERP.Modules.Purchasing/Features/OrderFeatures.cs:260-355): (1) `CreateBill` is saved, then the order (lines ~340-353) with no transaction; a failure between leaves a bill and `InvoicedBillId == null`, and a retry creates a duplicate bill. (2) The expense account for unreceived lines is the first postable expense account by code (line ~285); the variance fallback matches `Name.Contains("variance")` and `"cogs"`. (3) `UnitPriceOverrides` are not validated (negative, many decimals).

## Impact
Duplicate bills and payables, expenses booked to an arbitrary account, negative bill lines.

## Change wanted
- Run the handler in `IUnitOfWork.InTransactionAsync` and add `IRetryOnConcurrency` to `ConvertPurchaseOrderToBill`.
- Use a system-role account for unreceived lines (add a role such as `purchases.expense` to `SystemRoles` and the seeder, or reuse an existing one); if unset return 409 naming the role. Remove name/code-order fallbacks.
- Override prices must be >= 0 with at most 4 decimals; else 400 on `unitPriceOverrides`.

## Acceptance criteria
- [ ] If the order save fails after bill creation, no bill remains.
- [ ] Converting twice returns the same bill.
- [ ] No account is chosen by name or code order; a missing role gives a clear 409.
- [ ] Negative or 5-decimal override prices return 400.

## Tests required
Integration tests for each criterion; unit test for override validation.

## Do not touch
GR/IR clearing behaviour (TASK-008).

## What I did (Antigravity)

## Review (Claude)
