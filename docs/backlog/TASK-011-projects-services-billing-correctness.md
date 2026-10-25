---
id: TASK-011
title: Project and service billing must create invoices or fail, using customer terms
priority: P0
status: open
schema: no
depends_on: [TASK-003]
area: projects
audit_ref: AUDIT-DEEP B9, B10
commit:
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
- [ ] Billing a project without a customer returns 400 and flags nothing.
- [ ] Billed items always have an invoice id.
- [ ] Milestone not ReadyToInvoice cannot be invoiced.
- [ ] Milestone shows Paid only after the invoice is fully settled.

## Tests required
`ProjectsBillingTests`, `ServicesBillingTests` integration tests.

## Do not touch
Timesheet entry forms.

## What I did (Antigravity)

## Review (Claude)
