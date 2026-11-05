---
id: TASK-146
title: HR wave 4: statutory payroll rules, daily attendance, employee history, onboarding checklists, recruitment
priority: P1
status: ready-for-review
schema: yes
depends_on: [TASK-144]
area: hr
audit_ref: HR audit 2026-11-04 (tax and social insurance, daily attendance, history, onboarding, hiring)
commit:
owner: claude (branch claude/hr-wave1)
---

## What was built
- **Statutory rules** (`/hr/statutory-rules`): per employing entity, data not code. Flat percent of basic or gross with an optional threshold and cap, or progressive slabs; employee deduction or employer cost; by nationality (`!Qatari` = everyone but Qataris); effective dates. Payroll applies them to regular runs: the employee share becomes `StatutoryDeduction` (part of Deductions, never below zero net, taken before loan instalments), the employer share `EmployerCost` (not in net pay). Posting: employer cost Dr salaries / Cr payroll deductions payable; the employee share credits the same payable. `GET /hr/payroll/{id}/statutory` gives the per-rule totals for filing.
- **Daily attendance** (`/hr/attendance/daily`): check-in and check-out per day with late and overtime minutes (settings `hr.shiftStart`, `hr.lateGraceMinutes`, `hr.hoursPerDay`), bulk import (rows outside employment are rejected one by one), and a roll-up that writes the monthly record payroll already reads.
- **Employee history** (`/hr/employees/{id}/history`): joined, salary, designation, department, position, manager, contract, status and leaving are written automatically; manual notes; pay values are hidden without `hr.salary.read`. Reminders for probation and contract ends (30 and 7 days).
- **Onboarding and offboarding** (`/hr/checklists`, `/hr/employees/{id}/checklists/{templateId}`, `/hr/tasks`): reusable templates with day offsets, tasks per employee, done flag, open and overdue list.
- **Recruitment** (`/hr/openings`, `/hr/candidates`): vacancies, candidate pipeline (applied, screening, interview, offer; forward or out only), and `hire` which creates the employee through the normal employee rules and can start an onboarding checklist. Permissions `hr.recruitment.read` and `hr.recruitment.manage`.
- Migration `HrWave4`, script `0069_HrWave4.sql`.

## Screens (added after the backend)
Web: statutory rules, daily attendance (add a day, import, roll-up), history and tasks drawer on the employee, checklists, HR tasks, recruitment (vacancies, candidates, hire), statutory totals and employer cost on payroll detail. Mobile: the same, with history and tasks under the employee. Menu entries and Arabic phrases added; specs in `hr-wave5.spec.ts` and `mobile/test/hr_life_test.dart`.

## Half-day attendance (added later)
Status `halfDay` on a daily log: counts as attended, half a day unpaid in the roll-up. `UnpaidAbsentDays` is now decimal (migration `HrHalfDay`, script `0070_HrHalfDay.sql`).

## Not done
Shift rosters; photo or device integration; applying a country's actual tax tables (the rules are set up per entity by the user).

## Tests
`HrWave4Tests` (unit, 28 cases) and `HrWave4Tests` (integration, 6 scenarios); full backend suite green.
