---
id: TASK-024
title: Bills and payments accept only vendor parties
priority: P1
status: open
schema: no
depends_on: []
area: purchasing
audit_ref: Claude analysis, Procure to pay
commit:
---

## Problem
`BillHandlers.ApplyAsync` (`BillFeatures.cs`, `parties.FindAsync(i.PartyId)`) and `CreatePayment` (`PaymentFeatures.cs:72`) only check that the party exists. `Party.Roles` (`PartyRoles.Vendor`, `ERP.Domain/Parties`) is never consulted, so a customer-only or employee-only party can be billed and paid as a vendor.

## Impact
Customer or tenant records receive payables, vendor ageing and statements mix in unrelated parties, and payments can be issued to the wrong kind of party.

## Change wanted
- Add a small shared check (reuse an existing party helper if there is one, otherwise one static method in `ERP.Application/Parties`) that throws `FieldValidationException("partyId", "Choose a vendor.")` when the party has roles set and none of them is `Vendor`. A party with `Roles == None` stays allowed so existing data is not broken.
- Use it in `CreateBill`/`UpdateBill` (`ApplyAsync`) and `CreatePayment`.
- Do not change receivables.

## Acceptance criteria
- [ ] Creating a bill or payment for a party with only the Customer role returns 400 on `partyId`.
- [ ] A party with Vendor (alone or with other roles) and a party with no roles still work.
- [ ] Existing tests and seed data still pass (adjust fixtures that used a customer as vendor).

## Tests required
`PayablesTests`: customer-only party rejected for bill and payment; vendor accepted.

## Do not touch
Receivables, party CRUD.

## What I did (Antigravity)

## Review (Claude)
