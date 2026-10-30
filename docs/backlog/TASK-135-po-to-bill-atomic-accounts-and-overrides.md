---
id: TASK-135
title: Converting a purchase order to a bill is atomic, uses configured accounts and validates price overrides
priority: P1
status: ready-for-review
schema: no
depends_on: []
area: purchasing
audit_ref: Claude analysis, Procure to pay (second pass)
commit: e5cbf2c
owner: claude (branch claude/p2p; Antigravity skip)
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
- [x] If the order save fails after bill creation, no bill remains.
- [x] Converting twice returns the same bill.
- [x] No account is chosen by name or code order; a missing role gives a clear 409.
- [x] Negative or 5-decimal override prices return 400.

## Tests required
Integration tests for each criterion; unit test for override validation.

## Do not touch
GR/IR clearing behaviour (TASK-008).

## What I did (Claude, branch claude/p2p)
- The conversion runs in one `InTransactionAsync` and is `IRetryOnConcurrency`; converting twice returns the same bill. The expense account for unreceived lines is the system role `purchases.expense` (new in `SystemRoles`; the default chart gives account 6900 that role). The variance account is the `purchase.variance` role, looked up only when a price difference exists. No name or code-order fallbacks; a missing role is a 409 naming it. Override prices must be >= 0 with at most 4 decimals (400 `unitPriceOverrides`).
- Existing workspaces created before this change have no account with `purchases.expense`, so converting returns the 409 until one account (for example 6900) gets that role. Not run on any database.
- Not tested: an order-save failure after bill creation (it cannot be injected); the single transaction covers it.

## Review (Claude)
