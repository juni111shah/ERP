---
id: TASK-144
title: Staff loans and advances, termination with final settlement, sick-leave pay bands, department cost split in payroll
priority: P1
status: ready-for-review
schema: yes
depends_on: [TASK-140, TASK-141, TASK-142]
area: hr
audit_ref: HR audit 2026-11-04 (termination, loans, deductions, sick leave pay, cost centres)
commit:
owner: claude (branch claude/hr-complete)
---

## Problem
After TASK-140..143 payroll is correct and reversible, but: staff loans and advances are one flat "recurring deduction"; leaving an employee is a manual process (set status and date, then hand-calculate the final pay, leave payout and end-of-service amount); sick leave is either fully paid or unpaid, never banded; payroll expense is one lump with no department or cost-centre split.

## Change wanted
- **Loans and advances.** Entity `EmployeeLoan` (EmployeeId, Kind `Loan|Advance`, Principal, InstalmentAmount, StartMonth, Instalments, Remaining, Status `Active|Settled|Cancelled`, Reason). CRUD + `POST /hr/loans/{id}/cancel`. Payroll generation adds the instalment of every Active loan whose start month has come as a deduction line item `LoanRecovery` (new PayrollLine field, separate from `Deductions` for ordinary deductions); the posting credits `payroll.advances` for loan recovery and the deductions account for the rest (TASK-140). Instalments never exceed the remaining balance and never make net pay negative (the rest carries over); posting a run reduces `Remaining`, voiding a run restores it. Loan given out: `POST /hr/loans/{id}/disburse` (bank account, date) posts Dr `payroll.advances` / Cr bank. Permission `hr.payroll.manage` (disburse: `hr.payroll.pay`).
- **Termination.** `POST /hr/employees/{id}/terminate` (terminationDate, reason, lastWorkingDay optional): sets status Terminated and the date, cancels future-dated leave, and returns a settlement preview: pro-rated final salary (TASK-140 proration), unused leave encashment = remaining balance of paid leave types x daily rate (basic / days basis) using a per-type flag `LeaveType.Encashable`, end-of-service amount from `GratuityCalculator`, outstanding loan balance as a deduction, notice-period shortfall if `NoticePeriodDays` was not served (setting `hr.noticeShortfallDeduction`, default off). `POST /hr/employees/{id}/final-settlement` creates a `PayrollRun` of `Kind = FinalSettlement` with one line (new PayrollLine fields `LeaveEncashment`, `EndOfService`, `LoanRecovery`), posted through the normal post/pay flow, except that the end-of-service amount debits `payroll.gratuity.provision` (not the expense account) and any shortfall between the provision and the amount debits `payroll.gratuity.expense`. A terminated employee leaves regular runs after the final month.
- **Sick-leave pay bands.** `LeaveType.PayBands` (list of `{FromDay, PayPercent}`, for example day 1-15 at 100, 16-30 at 50, then 0) counted per employee per year of that leave type; `LeaveFeed` reports "unpaid-equivalent days" as a decimal and payroll's absence deduction uses it (so 50% pay days deduct half a day). Types without bands keep `IsPaid` behaviour.
- **Department cost split.** When employees have a department linked to a business unit (TASK-141), the payroll accrual journal lines carry `BusinessUnitId` (expense debit split per business unit; the liability credit stays one line). No change for employees without a department.

## Acceptance criteria
- [ ] A loan of 3,000 in 3 instalments is recovered 1,000 per month over three runs, ends Settled, never overdraws net pay, and voiding a run restores the balance; disbursement posts a balanced journal.
- [ ] Terminating mid-month gives a correct preview; the final-settlement run posts salary + leave payout + end of service - loans with the end-of-service part against the provision; the employee is excluded from later runs; re-running the preview is stable.
- [ ] A sick-leave type with bands deducts 0%, 50% and 100% of the daily rate over the right days and carries across months.
- [ ] Expense lines split by business unit and still balance; employees with no department behave exactly as before.

## Tests required
Unit tests for the recovery schedule, settlement maths and pay-band calculation; integration tests `HrLoansTests`, `HrTerminationTests`, `LeavePayBandTests`, `PayrollCostSplitTests`.

## Do not touch
Web and mobile screens (TASK-145); accounting rules outside payroll.

## What I did (Antigravity)

## Review (Claude)

### Claude, branch claude/hr-wave1
Built: `EmployeeLoan` + `PayrollLoanRecovery` (recovery applied at post, restored at void, never below zero net, rest carries over); `/hr/loans` (list, get, create, cancel, disburse); terminate, settlement preview and final settlement (`PayrollKind.FinalSettlement`, new line fields `LoanRecovery`, `LeaveEncashment`, `EndOfService`; end of service debits the provision first, the rest the expense account; the leaver is left off the regular run of the settled month); `LeaveType.Encashable` and `LeavePayBand` (banded sick pay counts fractional unpaid days in payroll); payroll expense split by the business unit of the employee's department. Setting `hr.noticeShortfallDeduction`. Migration `HrWave1`, script `0068_HrWave1.sql`. Tests: `HrWave1Tests` (unit and integration), full backend suite green.
