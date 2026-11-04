---
id: TASK-145
title: Web and mobile screens for the completed HR module
priority: P1
status: open
schema: no
depends_on: [TASK-140, TASK-141, TASK-142, TASK-143, TASK-144]
area: hr
audit_ref: HR audit 2026-11-04
commit:
owner: claude (branch claude/hr-complete)
---

## Problem
The backend features of TASK-140..144 have no screens. The web HR pages (`frontend/src/app/modules/hr`) and the mobile HR screens (`mobile/lib/features/hr`, `mobile/lib/features/purchasing`) only know the old model.

## Change wanted
**Web (Angular, follow the existing HR pages):** payroll detail: proration days, void with reason, pay selected employees, off-cycle runs, payslip PDF/email buttons, loan recovery and leave/end-of-service columns; employees: salary fields hidden without `hr.salary.read`, department/position/manager/user link, personal and contract fields, documents tab with expiry badges, loans tab, terminate dialog with the settlement preview and final-settlement action; departments and positions pages and an org chart; leave: half-day toggles, entitlement tiers and pay bands and attachment rule on leave types, attachment upload on a request, "apply country defaults"; a self-service area (my profile, my leave, my balances, my payslips) shown to linked users; HR reports page (headcount, joiners/leavers, payroll cost, leave, birthdays); document expiry list; settings for the new `hr.*` options.
**Mobile (Flutter, follow `mobile/docs/BUILDING_SCREENS.md`):** the same on the phone, prioritising self-service (my leave with half days and attachment, my balances, my payslips with PDF share), manager approvals, employee documents with expiry, loans, termination with settlement preview, payroll void/partial pay, and the HR reports.

## Acceptance criteria
- [ ] Every new endpoint of TASK-140..144 is reachable from a web screen and a mobile screen (or listed here as intentionally web-only with the reason).
- [ ] `ng build`, `flutter analyze` clean; frontend and mobile tests cover the new forms and actions; permissions hide actions the user may not use.
- [ ] Checked against a running API (throwaway database) before ready-for-review.

## Tests required
Spec tests for new Angular pages/forms; Flutter tests like `mobile/test/hr_*_test.dart`.

## Do not touch
Backend code except for a real bug found (report it).

## What I did (Antigravity)

## Review (Claude)
