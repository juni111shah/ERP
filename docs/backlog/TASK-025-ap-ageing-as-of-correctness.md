---
id: TASK-025
title: AP ageing and vendor statement must reflect the as-of date
priority: P1
status: open
schema: no
depends_on: []
area: finance-reports
audit_ref: Claude analysis, Procure to pay
commit:
---

## Problem
`ApReportHandlers.OpenAsync` (`ERP.Application/Finance/ApAgeing.cs`) filters `BillDate <= asOf` and status Issued/PartlyPaid, but uses the bill's current `Paid`. For a past `asOf`: payments made after that date are already subtracted, and a bill paid in full or voided after `asOf` is dropped entirely. `AdvancesAsync` likewise uses today's `Unallocated` and today's status with no date filter (payments dated after `asOf` count as advances).

## Impact
A month-end AP ageing re-run later does not reproduce the month-end numbers; it will not tie to the ledger payables balance at that date.

## Change wanted
- Compute the outstanding as of `asOf`: bill total minus allocations whose settlement `Date <= asOf` and that were not voided on or before `asOf`; include bills voided or paid after `asOf`; exclude bills voided on or before `asOf` (use `VoidedAt`).
- Advances: payments with `Date <= asOf`, minus allocations dated `<= asOf`, excluding payments voided on or before `asOf`.
- Use `IReadRepository` only (no DbContext).
- Check whether the AR ageing handler has the same flaw; if so, record it in this card's Review as a finding. Do not fix it here.

## Acceptance criteria
- [ ] Bill of 100 dated 1 Jan, paid on 15 Feb: ageing as of 31 Jan shows 100 outstanding, as of 28 Feb shows 0.
- [ ] A bill voided on 10 Feb still appears in the 31 Jan ageing and not in the 28 Feb one.
- [ ] A payment dated 15 Feb does not appear as an advance in the 31 Jan report.
- [ ] Ageing as of today is unchanged.

## Tests required
`PayablesTests`: the three scenarios above (set dates through the API).

## Do not touch
Report DTO shapes and endpoints.

## What I did (Antigravity)

## Review (Claude)
