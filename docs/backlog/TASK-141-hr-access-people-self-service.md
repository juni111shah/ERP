---
id: TASK-141
title: HR access and people: salary permission, segregation of duties, employee-user link, manager, self-service, structure, safe delete
priority: P0
status: ready-for-review
schema: yes
depends_on: []
area: hr
audit_ref: HR audit 2026-11-04 (items 2, 3, 5, 6, 11)
commit:
owner: claude (branch claude/hr-people)
---

## Problem
Verified in `backend/modules/ERP.Modules.Hr`:
- `EmployeeDto` and `EmployeeSummaryDto` (and the list endpoint) return basic, housing, transport and deductions to anyone holding `hr.employees.read`.
- A user can approve their own leave request; payroll can be generated, posted and paid by one person.
- `Employee` has no link to a user account and no line manager, so there is no "my leave / my payslip" and approvals cannot go to a manager. `RequestedByUserId` is only a stamp; anyone with `hr.leave.request` can file leave for any employee.
- `DeleteEmployee` hard-deletes with no check for payroll, leave, attendance or review history.
- Department is free text; there is no position, grade, contract or personal data; employee status never shows "on leave".

## Change wanted
- **Salary permission.** New permission `hr.salary.read` ("See salary and pay details"). Without it, salary fields of `EmployeeDto`/`EmployeeSummaryDto` (basic, housing, transport, other allowances, recurring deductions, gross, gratuity accrued, IBAN) are returned as null (make them nullable) and the list/sort/filter by those columns is disallowed. Payroll screens are unaffected (they have their own permissions). Add it to the HR manager role in `HrModule.cs` and to the "*" owner as usual. Update the DTOs, mapping and tests; mention in the card which existing tests changed.
- **Segregation of duties.** Settings `hr.blockSelfApproval` (bool, default `true`): approving or rejecting a leave request raised by the same user -> 409 "You cannot decide your own leave request." and `hr.payrollSeparationOfDuties` (bool, default `false`): when true, the user who generated or last adjusted a run cannot post it, and the user who posted it cannot pay it (409 with a clear message). Settings are per workspace, read like `hr.daysBasis`.
- **Employee-user link and manager.** `Employee.UserId` (Guid?, unique among employees of the workspace when set), `Employee.ManagerEmployeeId` (Guid?, cannot be itself, no cycles). Employee input/DTO carry both (user shown by name via the user directory). A user may be linked to one employee only (409 otherwise).
- **Self-service API.** Under `/api/v1/m/hr/me` (find the real module prefix in `HrEndpoints.cs`): `GET /me` (own employee record including own pay), `GET /me/leave` (own requests), `POST /me/leave` + submit/cancel own requests (employee id is taken from the link, never from the body), `GET /me/balances`, `GET /me/payslips` (own payroll lines of Posted/Paid runs: run number, month, status, gross, deductions, net, line detail), `GET /me/payslips/{lineId}`. Authenticated users with a linked employee need no extra permission; others get 404 "No employee is linked to your account." Staff without `hr.leave.manage` (new permission, "File leave for any employee") can only file leave for their own linked employee: `CreateLeaveRequest`/`UpdateLeaveRequest` for someone else -> 403/409 accordingly. HR managers get `hr.leave.manage`.
- **Manager approval.** When a leave request is submitted, notify the employee's manager's user first (if linked); a manager may approve/reject their direct reports' requests even without `hr.leave.approve`. Others still need `hr.leave.approve`.
- **Employee structure and personal data.** New master data `Department` (code, name, parent department, head employee, optional `BusinessUnitId`, active) and `Position` (code, title, grade text, active) with list/create/update/delete (delete blocked when employees use it) and permissions `hr.employees.manage`. `Employee` gets optional `DepartmentId`, `PositionId` (the free-text `Department`/`Designation` stay and are filled from the master when one is chosen), plus `DateOfBirth`, `Gender`, `MaritalStatus`, `EmergencyContactName`, `EmergencyContactPhone`, `Address`, `ContractType` (Permanent, FixedTerm, Probation, PartTime, Contractor), `ProbationEndDate`, `ContractEndDate`, `NoticePeriodDays`, `BankName`. Validators: contract end after join; probation end after join. Employees list can filter by department.
- **Safe delete + on leave.** `DeleteEmployee` returns 409 "This employee has payroll, leave, attendance or review history. Terminate the employee instead." when any exists (delete still allowed otherwise). `EmployeeSummaryDto` gets `OnLeaveToday` (bool) computed from approved leave covering today; do not change the stored `Status`.
- `GET /hr/org-chart`: departments and employees as a tree (manager relation) for the UI.

## Acceptance criteria
- [ ] A user with `hr.employees.read` but not `hr.salary.read` sees null pay fields (detail and list); with it, values as before.
- [ ] Approving your own leave request is 409; another approver succeeds; the setting off allows it.
- [ ] With `hr.payrollSeparationOfDuties` on: generator cannot post, poster cannot pay; a second user can.
- [ ] A linked user can create, submit and cancel their own leave and read their balances and payslips; cannot see anyone else's; an unlinked user gets 404; filing for another employee without `hr.leave.manage` is refused.
- [ ] A manager can approve a direct report's request without `hr.leave.approve`.
- [ ] Departments and positions can be created, used on employees and not deleted while used; manager cycles are rejected.
- [ ] Deleting an employee with history is 409; without history works; `OnLeaveToday` is true only on approved leave days.

## Tests required
Integration tests in new `HrAccessTests` and `HrSelfServiceTests` (use the sign-in helpers in `ApiFactory`; create users with limited roles) for every criterion; unit tests for the org-tree builder / cycle check.

## Do not touch
Payroll calculation and posting (TASK-140), leave balance logic (TASK-142), documents/WPS (TASK-143), web and mobile screens.

## What I did (Claude, branch claude/hr-people)
All acceptance criteria are covered by integration tests (`HrAccessTests`, `HrSelfServiceTests`) and unit tests (`HrOrgTreeTests`). No migration was created (the lead generates one): new columns on `Employee` and `PayrollRun` (`GeneratedByUserId`, `LastAdjustedByUserId`, `PostedByUserId`), new tables `hr_Departments` and `hr_Positions`, a unique filtered index on `Employee (OrgId, UserId)`.

Decisions
- Salary: `hr.salary.read` and `hr.leave.manage` added (HR manager role has both). Pay fields of `EmployeeDto`/`EmployeeSummaryDto` are nullable and null without the permission (incl. IBAN, gross, gratuity accrued). The list is now a custom `ListEmployees` request (the pay columns are not even projected without the permission); sort/filter by pay columns was already refused (not whitelisted, 400). `EmployeeInput` pay fields are nullable: callers without `hr.salary.read` cannot change pay on update (ignored), a missing pay value on update keeps the stored one; create still needs a basic salary.
- Self-approval: setting `hr.blockSelfApproval` (default on) blocks approve/reject when the request was raised by the caller or belongs to the caller's linked employee (409 "You cannot decide your own leave request."). `ApproveLeaveRequest`/`RejectLeaveRequest` no longer implement `IRequirePermission`; the endpoint needs sign-in and the handler requires `hr.leave.approve` or being the manager of the request's employee (403 otherwise).
- Payroll separation of duties: setting `hr.payrollSeparationOfDuties` (default off). Explicit stamps on `PayrollRun` (generator, last adjuster, poster) through a small `PayrollSeparation` service; the check sits in post and pay only. A preparer cannot even start the approval submission of a run, a second user must post.
- Manager: `Employee.ManagerEmployeeId` (not itself, no cycles, 409), `UserId` (workspace member, one employee per account, 409). Submitting leave notifies the manager's user first, then the other approvers. Extra endpoint `GET /me/team/leave?status=` so a manager without `hr.leave.read` can find the requests to decide.
- Self-service under `/api/v1/m/hr/me` (`me`, `me/leave`, `me/leave/{id}/submit|cancel`, `me/balances`, `me/payslips`, `me/payslips/{lineId}`): authenticated only, no permission; the leave logic reuses `LeaveHandlers`. Another person's request/line is 404.
- Filing for others: `CreateLeaveRequest`/`UpdateLeaveRequest` for an employee not linked to the caller need `hr.leave.manage` (403). Submit/cancel on the general endpoints are unchanged (not in the card).
- Structure: `Department`, `Position` CRUD with `hr.employees.read`/`manage`, delete blocked while used (departments also while they have sub-departments), parent cycles 409. Choosing a department/position fills the free-text `Department`/`Designation`. New personal and contract fields on the employee; validators for contract and probation end. `GET /hr/org-chart` returns the department tree with members, unassigned people and a manager reporting tree.
- Delete employee: 409 when payroll lines, leave, attendance or reviews exist. `OnLeaveToday` on list and detail from approved leave covering today (status untouched).

Existing tests changed: `HrLeaveTests.SetupAsync` now sets `hr.blockSelfApproval` to false because those tests create and approve with one account. No other existing test needed a change (the owner holds every permission).

Not done / notes
- Department `BusinessUnitId` is a plain optional id checked on save (no database foreign key).
- Submit/cancel of other people's requests on the general `/leave` endpoints are not restricted (card only asked for create/update).
- The manager notification is covered indirectly; no test asserts the notification row.
- Test run: whole solution passes except 5 `PosSessionTests` that need SQL Server (not available here, unrelated).

## Review (Claude)
