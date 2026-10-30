---
id: TASK-136
title: Purchase order vendor, currency, dates and precision are validated
priority: P2
status: ready-for-review
schema: no
depends_on: []
area: purchasing
audit_ref: Claude analysis, Procure to pay (second pass)
commit: e5cbf2c
owner: claude (branch claude/p2p; Antigravity skip)
---

## Problem
`PurchaseOrderInputValidator` (backend/modules/ERP.Modules.Purchasing/Features/OrderFeatures.cs:101-117) and the handlers accept a `VendorPartyId` that is never loaded (any GUID, a customer, an employee), a free-text `VendorName` that may differ from the party, any 1-10 character `Currency` (the bill later uses the entity currency), no rule for `OrderDate`/`ExpectedDeliveryDate` (0001-01-01, delivery before order), and unlimited decimals on quantity and unit price.

## Impact
Orders for non-vendors, wrong currency labels, impossible dates, totals drifting from line prices.

## Change wanted
- When `VendorPartyId` is given, load it, apply `VendorRules.RequireVendor`, and set `VendorName` from the party.
- `Currency` must equal the legal entity's base currency; 400 on `currency` otherwise.
- `OrderDate`/`ExpectedDeliveryDate` use `MustBePlausibleDate`; delivery not before order date.
- Quantity and unit price at most 4 decimals.

## Acceptance criteria
- [x] Customer-only party, unknown party id, wrong currency, year-1 date, delivery before order date and a 5-decimal price each return 400 with the matching key.
- [x] Orders for a normal vendor still work.

## Tests required
Integration tests for each rule.

## Do not touch
Order numbering.

## What I did (Claude, branch claude/p2p)
- The vendor party is loaded and checked with `VendorRules.RequireVendor`, and the stored vendor name comes from the party. Currency must equal the legal entity base currency (400 `currency`). Order and delivery dates are plausible, delivery not before order. Quantity and unit price have at most 4 decimals.
- Tests: `PurchaseOrderRulesTests` for each rule and key; a normal vendor still works.

## Review (Claude)
