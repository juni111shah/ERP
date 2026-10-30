---
id: TASK-031
title: Sales use the register's warehouse and cash account; inactive registers cannot trade
priority: P1
status: done
schema: no
depends_on: []
area: pos
audit_ref: POS analysis 2026-10-30
commit: 054f300
---

## Problem
- `PosRegister.WarehouseId` and `DefaultCashAccountId` exist (`RegisterFeatures.cs:12-14`) but are never used. Stock is issued without a warehouse (`OrderFeatures.cs:475-481`, `StockIssueRequest.WarehouseId` left null) and the cash account always comes from the `pos.cash` role (`441`).
- `IsActive` is not checked when opening a session (`SessionFeatures.cs:138`) or creating an order, and a register can be deactivated or deleted while a session is open (`RegisterFeatures.cs:85-101`).
- The order's `RegisterId` and `LegalEntityId` are never compared with the session's (`OrderFeatures.cs:208-215`), so an order can be posted to another legal entity's ledger.

## Impact
Stock leaves the wrong warehouse, per-till cash accounts are ignored, retired tills keep selling, deleting a register orphans an open shift, and cross-entity postings are possible.

## Change wanted
- Load the register in `CreatePosOrderCommand`; pass `register.WarehouseId` in `StockIssueRequest`; use `DefaultCashAccountId` for cash lines when set, otherwise the existing role fallback.
- `OpenSessionCommand` and `CreatePosOrderCommand` throw `ConflictException` when the register is inactive.
- `UpdateRegisterCommand` (setting `IsActive=false`) and `DeleteRegisterCommand` throw `ConflictException` while an open session exists.
- Reject (400) an order whose `RegisterId` or `LegalEntityId` differs from its session's, and a session whose `LegalEntityId` differs from its register's.

## Acceptance criteria
- [ ] A sale on a register with a warehouse creates stock movements in that warehouse.
- [ ] The cash debit goes to the register's `DefaultCashAccountId` when set.
- [ ] Inactive register: open session 409, create order 409.
- [ ] Delete or deactivate with an open session returns 409.
- [ ] Order with a register or legal entity that does not match its session returns 400.

## Tests required
Integration tests in `PosOrderTests` and a new `PosRegisterTests`, one per criterion.

## Do not touch
Void flow, price and payment validation.

## What I did (Claude)
Order create loads the register: stock issues from its WarehouseId, cash debit uses DefaultCashAccountId (else pos.cash role). OpenSession/CreateOrder reject inactive registers (409); Update(deactivate)/Delete reject while a session is open (409); order or session with a mismatching register/legal entity returns 400. CreateRegisterCommand.IsActive now defaults to true (an omitted isActive used to create an inactive till, which now blocks trading). Tests: PosRegisterTests.

## Review (Claude)
- Verified: dotnet build 0 errors; 253 unit + 285 integration pass, 4 skipped (SQL Server).
