---
id: TASK-126
title: Invoices and receipts accept only customer parties
priority: P1
status: in-progress
schema: no
depends_on: []
area: sales
audit_ref: Claude analysis, Order to cash (second pass)
commit:
owner: claude (worktree work-claude; Antigravity skip)
---

## Problem
`InvoicingService.ApplyAsync` (backend/src/ERP.Application/Finance/Invoicing.cs:54) and `ReceiptHandlers.Handle(CreateReceipt)` (backend/src/ERP.Application/Finance/ReceiptFeatures.cs:76) load the party but never check its role, so an invoice or a receipt can be created for a vendor or an employee. Bills already check this (`VendorRules.RequireVendor`, TASK-024).

## Impact
Receivables and ageing contain vendors and employees; customer statements and the portal show them; party ledger totals mix AR and AP.

## Change wanted
- Add `CustomerRules.RequireCustomer(Party)` next to `VendorRules` (backend/src/ERP.Application/Parties): a party with roles set must include `Customer`; a party with `PartyRoles.None` stays allowed (same compatibility rule as vendors). Error: `FieldValidationException` on `partyId`, "Choose a customer."
- Call it in `InvoicingService.ApplyAsync` and in `CreateReceipt`. Credit notes use the same path and get the check too.

## Acceptance criteria
- [ ] Creating or updating an invoice or credit note for a vendor-only party returns 400 with an error on `partyId`.
- [ ] Creating a receipt for a vendor-only or employee-only party returns 400 with an error on `partyId`.
- [ ] A party with roles Customer|Vendor, and one with no roles, still work.
- [ ] Module billing runs for customers (real estate, projects, sales orders) still pass their tests.

## Tests required
Integration tests for invoice, credit note and receipt with a vendor-only party (400), a Customer|Vendor party (OK) and a role-less party (OK).

## Do not touch
Accounting rules, journal shapes and numbering; module billing code (it calls `IInvoicingService` directly).

## What I did (Antigravity)

## Review (Claude)
