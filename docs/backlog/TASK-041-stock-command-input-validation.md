---
id: TASK-041
title: Validate dates, ids and precision on stock receive, issue, adjust and transfer
priority: P2
status: done
schema: no
depends_on: []
area: inventory
audit_ref: Claude analysis, Inventory and Manufacturing
commit: 5a4362c
---

## Problem
The validators in `StockFeatures.cs` check only quantity and cost signs. `Date` can be `0001-01-01` (the default when the field is omitted), which posts a ledger journal dated year 1 for receive/issue/adjust. `TransferStock` has no `NotEmpty` on warehouse ids or item id. Quantity and unit cost accept more than 4 decimals although the table keeps 4 and the journal rounds to 2 (`Math.Round(i.Quantity * i.UnitCost, 2)`), and quantities are unbounded.

## Impact
Journals and movements with year-1 dates, silent rounding differences between the stock ledger and the general ledger, and unclear errors for missing ids.

## Change wanted
- Add to the receive, issue, adjust and transfer validators: `Date` must not be the default value and must be within 10 years of today; ids `NotEmpty` (including transfer); `Quantity` and `QuantityDelta` at most 4 decimals and under 1,000,000,000; `UnitCost` at most 4 decimals and under 1,000,000,000.
- Use a small shared `PrecisionRules` extension in `StockFeatures.cs` (or reuse `MustBeMoney` style helpers in `ERP.Application.Common` if one fits decimals with 4 places).

## Acceptance criteria
- [ ] Omitting `date` on receive/issue/adjust/transfer returns 400 on `date`.
- [ ] A quantity of 1.00001 or a unit cost of 1.00001 returns 400.
- [ ] Transfer with an empty item or warehouse id returns 400.
- [ ] Valid requests still return 201.

## Tests required
`InventoryTests`: one test per rule.

## Do not touch
Handler logic and permissions.

## What I did (Claude, on user request)
Added StockRules (ValidStockDate, FourDecimals) to the four stock validators and NotEmpty ids on transfer. Test: missing dates, empty ids, 5-decimal quantity/cost rejected; valid receipt accepted.

## Review (Claude)
- Verified: dotnet build 0 errors; dotnet test 253 unit + 283 integration pass, 4 skipped (SQL Server).
