---
id: TASK-140
title: Payroll core: proration for joiners and leavers, void a posted run, pay in parts, off-cycle runs, deductions to the right account
priority: P0
status: in-progress
schema: yes
depends_on: []
area: hr
audit_ref: HR audit 2026-11-04 (items 1, 4, 8)
commit:
owner: claude (branch claude/hr-complete)
---

## Problem
Verified in `backend/modules/ERP.Modules.Hr`:
1. `PayrollHandlers.BuildLinesAsync` + `PayrollMath.Calculate` pay a full month to anyone for whom `Employee.IsPayableIn` is true, including someone who joined on the 28th or left on the 3rd.
2. A run goes Draft -> Posted -> Paid with no way back: a mistake after posting needs manual journals. Paying is all-or-nothing.
3. `GeneratePayroll` refuses a second run for the same entity and month, so bonus runs and corrections are impossible.
4. Every recurring deduction is credited to the `payroll.advances` account (an asset), whatever the deduction really is.

## Change wanted
- **Proration.** For a month with D calendar days, an employee employed on E of them (join date / termination date inside the month) gets basic and allowances x E/D. Add `PayrollLine.ProratedDays` (int, calendar days paid, default = D) and show it. Overtime is not prorated. Absence keeps today's rule (full monthly gross / daysBasis x unpaid days). Line earnings = prorated basic and allowances - absence + overtime + adjustment; if that would be negative the existing "would make the pay negative" error applies. Put the rule in `PayrollMath` (pure, unit tested). A termination date before the month start or a join date after the month end is not payable (already true).
- **Void.** New `PayrollStatus.Voided` (value 4; keep existing numeric values) and `POST /hr/payroll/{id}/void` with a required reason (use `VoidMemo`, `ValidVoidReason`, `IVoidable` like invoices). Voiding a Posted run reverses the accrual journal with `IPostingService.ReverseTodayAsync`; voiding a Paid or PartiallyPaid run reverses the payment journal(s) first, then the accrual. Voided runs free the month for a new Regular run. Permission: `hr.payroll.post`. Write an audit event like the other voids.
- **Pay in parts.** `PayPayroll` gets an optional list of employee ids (default = every unpaid line). Add `PayrollLine.PaidOn` and `PayrollLine.PaymentJournalId`; `PayrollStatus.PartiallyPaid` (value 5) while some but not all lines are paid; `Paid` when all are. `PayrollRun.PaymentJournalId` / `PaidOn` keep the last payment for compatibility. Each payment posts Dr payroll.accrued / Cr bank for the selected lines' net.
- **Off-cycle runs.** `PayrollRun.Kind` enum `Regular = 0, OffCycle = 1` (value 2 `FinalSettlement` is reserved for TASK-144). Only Regular runs are limited to one per entity and month (non-voided). Generate input gets `Kind` and, for OffCycle, an optional list of employee ids and a required note; an off-cycle run contains only the adjustment amounts typed on its lines (no basic/allowances unless the employee is included with `IncludeSalary = true`). Keep it simple: off-cycle lines start at zero and are filled with `AdjustPayrollLine`.
- **Deduction account.** Recurring deductions credit the account with role `payroll.deductions` when the workspace has one; otherwise they keep going to `payroll.advances` (no behaviour change for existing workspaces). Add account `2310 Other payroll deductions payable` with role `payroll.deductions` to the default chart template (`backend/src/ERP.Application/Templates/Templates.cs`), and mention in the card note that existing workspaces need that account added to switch.
- The bank file and WPS file (`GetPayrollBankFile`, `GetPayrollWpsFile`) must skip lines already paid when asked for the unpaid part, and refuse Voided runs. Keep their current output otherwise.

## Acceptance criteria
- [ ] Joining on 2026-10-21 (31-day month) pays 11/31 of basic and allowances; leaving on 2026-10-10 pays 10/31; a full-month employee is unchanged; `ProratedDays` is on the line and in the DTO.
- [ ] Voiding a Posted run reverses its journal (trial balance back to the prior state) and a new Regular run for that month can be generated; voiding a Paid run reverses payment and accrual; a reason is required (400 without); voiding twice is 409.
- [ ] Paying two of three lines leaves the run PartiallyPaid with a journal for exactly those two nets; paying the rest makes it Paid; paying a paid line again is 409.
- [ ] An OffCycle run can exist next to the Regular run of the same month; a second Regular run is still 409.
- [ ] With a `payroll.deductions` account, recurring deductions credit it; without one they credit `payroll.advances` as before. Journals always balance.
- [ ] Existing HR tests still pass (update the ones that assumed the old behaviour and say which in the card).

## Tests required
Unit tests for `PayrollMath` proration (31-, 30- and 28-day months, join and leave in the same month, leave before join is not payable). Integration tests in a new `PayrollCoreTests` for every criterion above.

## Do not touch
Attendance and leave logic, employee master data, web and mobile screens (a later wave).

## What I did (Antigravity)

## Review (Claude)
