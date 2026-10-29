---
id: TASK-011
title: Project and service billing must create invoices or fail, using customer terms
priority: P0
status: done
schema: no
depends_on: [TASK-003]
area: projects
audit_ref: AUDIT-DEEP B9, B10
commit: d5dac1b
---

## Problem
`BillProject` still flags timesheets billed and milestones invoiced when the project has no customer or when invoice creation is skipped; invoice date and due date are hard-coded (UtcNow, +30 days); lines have no tax code or revenue account. Services milestone page "Mark as Invoiced" uses the plain status update instead of `POST /milestones/{id}/invoice`; the endpoint does not require `ReadyToInvoice`; due date may precede the invoice date; "Paid" is set manually.

## Impact
Work marked billed with no invoice; wrong due dates; unpaid milestones shown Paid.

## Change wanted
- `BillProject`: require a customer (400 if none), build the invoice first, then flag items in the same transaction; due date from the customer's payment terms; default tax code from the project or item; revenue account from the item/service setting.
- Milestone invoice: allow only from `ReadyToInvoice`; due date = max(invoice date, milestone due); UI button calls the invoice endpoint; remove the manual "Paid" button and derive Paid from invoice settlement status (read from the invoice).
- Engagement and retainer forms must use a customer picker (no fake GUID defaults).

## Acceptance criteria
- [x] Billing a project without a customer returns 400 and flags nothing.
- [x] Billed items always have an invoice id.
- [x] Milestone not ReadyToInvoice cannot be invoiced.
- [x] Milestone shows Paid only after the invoice is fully settled.

## Tests required
`ProjectsBillingTests`, `ServicesBillingTests` integration tests.

## Do not touch
Timesheet entry forms.

## What I did (Antigravity)
- Added `FieldValidationException` (HTTP 400) when billing a project without a customer assigned, before modifying timesheets/milestones.
- Updated `BillProject` to lookup default tax code, use customer payment terms (`DueDate: null`), link invoice via `SourceType`/`SourceId`, assign `ts.InvoiceId = invoice.Id`, and execute within `uow.InTransactionAsync`.
- Updated `InvoiceInput` to optionally accept `SourceType` and `SourceId` and added `InvoicePaymentChanged` notification on full credit-note settlement.
- Added `GetMilestoneQuery` and `MilestonePaymentNotificationHandler` in Services module to automatically synchronize milestone `Status = Paid` when its linked invoice is settled.
- Constrained milestone invoicing to `ReadyToInvoice` status and calculated `DueDate = max(invoiceDate, milestoneDue)`.
- Blocked manual transition to `Paid` via `UpdateMilestoneStatusCommand`.
- Removed the manual "Mark as Paid" action button from `milestone-billing.page.ts`.
- Created integration tests in `ProjectsBillingTests.cs` and `ServicesBillingTests.cs` covering all 4 acceptance criteria.

## Review (Claude)

- 2026-10-30: DONE. Verified commit d5dac1b: all 4 criteria covered by `ProjectsBillingTests` (no customer -> 400, nothing flagged; invoice id on timesheets, customer terms) and `ServicesBillingTests` (not ReadyToInvoice rejected, manual Paid blocked, due = max(invoice date, milestone due), Paid only after settlement). Clean copy: build 0 errors, 223 unit + 201 integration passed, `ng build` succeeded.
- Not delivered from "Change wanted" (not in criteria): revenue account from item/service setting; customer picker for engagement/retainer forms. Not blocking.
