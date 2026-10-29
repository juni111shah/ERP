---
id: TASK-016
title: Invoice generated from a sales order keeps tax, source link and currency
priority: P0
status: open
schema: no
depends_on: []
area: sales
audit_ref: order-to-cash review 2026-10-30
commit:
---

## Problem
`ConvertOrderToInvoice` (`backend/modules/ERP.Modules.Sales/Features/OrderFeatures.cs`, ~lines 300-325) builds `InvoiceLineInput(Description, Quantity, Price, ItemId)` and never passes `TaxCodeId`. `SalesOrderLine` only has a numeric `TaxRate` (`Domain/SalesModels.cs:173`). `InvoicingService.ApplyAsync` (`ERP.Application/Finance/Invoicing.cs:75`) computes tax only from a tax code, so the invoice has `Tax = 0` while the order has `TaxTotal > 0`. The invoice is also created without `SourceType`/`SourceId`, and `InvoicingService` forces `CurrencyCode = entity.BaseCurrency` (line 83) even when `order.Currency` differs.

## Impact
Customer is invoiced without tax (output tax never posted to the TaxOutput account); invoice total differs from the order total; a foreign-currency order is invoiced silently in base currency; the invoice cannot be traced back to its order.

## Change wanted
- In `ConvertOrderToInvoice`, resolve a `TaxCode` of the order's legal entity whose `Rate` equals each line's `TaxRate` (rate 0 -> no code) and pass `TaxCodeId`. If a line has a rate > 0 and no tax code matches, throw `ConflictException` naming the line and rate (do not invoice).
- Pass `SourceType: "salesorder"` and `SourceId: order.Id` in `InvoiceInput`.
- If `order.Currency` differs from the legal entity base currency, throw `ConflictException("Foreign-currency sales orders cannot be invoiced yet.")` (explicit failure instead of a silent base-currency invoice).
- After creation, compare `invoice.Total` with `order.TotalAmount` (allow at most 0.01 per line rounding difference); otherwise throw `ConflictException` and do not mark the order invoiced.

## Acceptance criteria
- [ ] Converting a 5% taxed order produces an invoice with Tax = order.TaxTotal and Total = order.TotalAmount.
- [ ] The invoice has SourceType `salesorder` and SourceId = order id.
- [ ] A foreign-currency order, or a rate with no matching tax code, returns 409, creates no invoice and leaves the order status unchanged.

## Tests required
Integration tests in `ERP.IntegrationTests` (Sales): taxed order totals match; unmatched rate -> 409 and no invoice row; foreign currency -> 409.

## Do not touch
Invoice posting rules, tax code rates, POS.

## What I did (Antigravity)

## Review (Claude)
