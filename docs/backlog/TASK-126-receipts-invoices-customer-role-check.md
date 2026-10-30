---
id: TASK-126
title: Invoices and receipts accept only customer parties
priority: P1
status: done
schema: no
depends_on: []
area: sales
audit_ref: Claude analysis, Order to cash (second pass)
commit: 710861a
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
- [x] Creating or updating an invoice or credit note for a vendor-only party returns 400 with an error on `partyId`.
- [x] Creating a receipt for a vendor-only or employee-only party returns 400 with an error on `partyId`.
- [x] A party with roles Customer|Vendor, and one with no roles, still work.
- [x] Module billing runs for customers (real estate, projects, sales orders) still pass their tests.

## Tests required
Integration tests for invoice, credit note and receipt with a vendor-only party (400), a Customer|Vendor party (OK) and a role-less party (OK).

## Do not touch
Accounting rules, journal shapes and numbering; module billing code (it calls `IInvoicingService` directly).

## What I did (Claude, implemented in separate worktree)
- Added `CustomerRules.RequireCustomer` beside `VendorRules`: a party with roles must include Customer; a role-less party stays allowed. 400 on `partyId`, "Choose a customer."
- Called it in `InvoicingService.ApplyAsync` (covers create/update invoice and credit note) and in `CreateReceipt`. Module billing is unaffected (their tests pass).
- Tests: `OrderToCashInputTests` (invoice and credit note for vendor-only and employee-only -> 400; receipt -> 400; update to a vendor -> 400; Customer and Customer+Vendor still work) and a unit test of the rule for every role combination. The API refuses to create a role-less party, so that case is covered by the unit test only.

## Review (Claude)

Verified in commit 710861a: `CustomerRules.RequireCustomer` called in `InvoicingService.ApplyAsync` (covers invoice, update, credit note) and `CreateReceipt`; role-less and Customer|Vendor parties stay allowed. Integration tests cover vendor/employee rejection (invoice, credit note, receipt, update) and the allowed parties.
Clean `git archive` copy of HEAD (7479c1b): `dotnet build backend/ERP.sln` 0 errors; `dotnet test`: unit 286 passed, integration 364 passed, 8 skipped (SQL Server only), 0 failed. Reviewed 2026-10-30.
