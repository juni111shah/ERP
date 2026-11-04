---
id: TASK-142
title: Leave policy: service-based entitlement, pro-rata, half days, year-end split, medical certificate, country defaults
priority: P1
status: ready-for-review
schema: yes
depends_on: []
area: hr
audit_ref: HR audit 2026-11-04 (items 9, 10 and leave gaps)
commit:
owner: claude (branch claude/hr-leave)
---

## Problem
Verified in `LeaveFeatures.cs`, `Leave.cs`, `HrModule.cs`:
- `LeaveType.AnnualDays` is one flat number for everybody; `BalanceAsync` creates `Entitled = type.AnnualDays`. No service-based tiers, no pro-rata for people who join mid-year, no monthly accrual.
- A request spanning 28 Dec to 5 Jan takes all its days from the start year (`FromDate.Year`).
- No half-day leave; `LeaveRequest.Days` is a decimal but nothing produces 0.5.
- No supporting document (medical certificate) rule.
- A new workspace has no leave types and no public holidays; country packs (`backend/countrypacks/*`) cannot supply them.

## Change wanted
- **Entitlement tiers.** New entity `LeaveEntitlementTier(LeaveTypeId, FromServiceYears decimal, Days decimal)`; the entitlement for a year is the tier with the highest `FromServiceYears` <= the employee's service years at 1 January of that year (or at the join date in the join year), else `AnnualDays`. CRUD through the leave type input (list of tiers). Show the tier-based entitlement in `GetLeaveBalances`.
- **Pro-rata and accrual.** `LeaveType.ProRataFirstYear` (bool): in the join year entitlement = annual x remaining months/12 (round to the nearest half day). `LeaveType.AccrualMode` enum `Upfront = 0, Monthly = 1`: with Monthly only the months elapsed so far (to today, or to the end of the request's month when booking ahead is not allowed) are available. Keep default Upfront so nothing changes.
- **Half days.** `LeaveRequest.StartHalfDay` and `EndHalfDay` (bool): the first and/or last day counts 0.5; a single-day request with either flag is 0.5. `Days` is computed by the server (keep the existing working-day rules for weekends and holidays; a half day on a non-working day is rejected).
- **Year-end split.** When a request crosses 31 Dec, the days in each calendar year are deducted from that year's balance on approval and restored per year on cancel; the balance check applies per year. Keep `LeaveRequest.Days` as the total; add `LeaveRequestYearDays(RequestId, Year, Days)` rows for the split (or compute it deterministically from the dates, your choice, but approvals made before this change must keep working: treat requests with no rows as single-year).
- **Supporting document.** `LeaveType.RequiresAttachmentAfterDays` (int?, null = never): a request longer than that many days cannot be submitted without at least one attachment (409 "Attach a medical certificate before submitting."). Use the existing attachment mechanism: see `IAttachmentOwnerResolver` (TASK-001, `backend/src/ERP.Application/Workflow/`) and how another module registers an owner type; register `hr.leaverequest` so files are authorized by `hr.leave.read` for reading and `hr.leave.request` for adding.
- **Country defaults.** Extend the country-pack contract in the core only with generic, country-neutral shapes, e.g. `ICountryPack.LeaveDefaults` (list of leave type templates: code, name, isPaid, annualDays, tiers, carry cap, requires attachment after N days) and `PublicHolidayTemplates` (name plus either a fixed month/day or a year -> date function). Country-specific content lives ONLY in the packs (`backend/countrypacks/ERP.CountryPacks.QA`, `.AE`, others if easy). The repo has a core-vocabulary denylist unit test: keep country words out of `backend/src`. Qatar: annual leave 21 days, 28 after 5 years of service; sick leave with certificate after 2 days; maternity 50 days (paid); bereavement; Hajj (unpaid, once); National Day 18 December and Sports Day (second Tuesday of February); variable Eid dates are entered by the admin each year, not seeded. UAE: annual 30 days, sick, maternity, hajj, bereavement; National Day 2-3 December, New Year 1 January. Add `POST /hr/leave/apply-country-defaults?year=` (permission `hr.leave.approve`), idempotent: creates only missing leave types (by code) and holidays (by date), returns what it added. Also apply automatically the first time the HR module is enabled for an empty workspace if the module-enable hook makes that easy; otherwise only the endpoint.

## Acceptance criteria
- [x] Tiers: 21 days with 2 years of service, 28 with 6 years; year of joining pro-rata gives half-day rounding; monthly accrual limits what can be booked.
- [x] A half-day request is 0.5 and half-day start+end across 3 days is 2.0; a half day on a weekend/holiday is rejected.
- [x] A 28 Dec to 5 Jan request deducts the right days from each year's balance on approval and gives them back on cancel; insufficient balance in either year is 409.
- [x] A 3-day sick request without attachment cannot be submitted; with an attachment it can; a 2-day one can without.
- [x] `apply-country-defaults` on a Qatar and a UAE entity creates the expected types and holidays and a second call creates nothing; the denylist test and all existing tests pass.

## Tests required
Unit tests for the entitlement/pro-rata/half-day/year-split calculators (pure functions, put them in a testable static class). Integration tests in new `LeavePolicyTests` for each criterion.

## Do not touch
Payroll calculation and posting (TASK-140), employee master data and permissions (TASK-141, except reading `JoinDate`), documents/WPS (TASK-143), web and mobile screens. Sick-leave pay tiers (half pay etc.) are TASK-144, not this card.

## What I did (Claude, branch claude/hr-leave)
Commit: see `git log --grep TASK-142` (last commit on the branch).
- Pure calculator `Features/LeaveEntitlement.cs` (service years, tiers, pro-rata to half day, monthly accrual, half days, per-year split); DB parts in `Features/LeavePolicyService.cs`; country defaults in `Features/LeaveDefaults.cs`; new entities `LeaveEntitlementTier`, `LeaveRequestYearDays` in `Domain/LeavePolicy.cs`; new columns on `LeaveType` (ProRataFirstYear, AccrualMode, RequiresAttachmentAfterDays) and `LeaveRequest` (StartHalfDay, EndHalfDay). No migration (lead generates it).
- Decisions: tiers apply at 1 January (at the join date in the join year); pro-rata counts the join month; Monthly accrual counts the months up to today or the month of the request's last day, whichever is later; year split is stored as rows written on approval, requests with no rows are treated as single-year (old approvals keep working); balance is only enforced when AnnualDays > 0 (tiers override AnnualDays but a type with AnnualDays 0 stays unenforced); `hr.leaverequest` attachment owner uses `hr.leave.read` / `hr.leave.request`; the 409 check also covers the draft path (upload to the draft, then submit).
- Country contract: `ICountryPack.LeaveDefaults` and `PublicHolidayTemplates` (generic). Qatar and UAE content only in the packs. `POST /hr/leave/apply-country-defaults?year=&legalEntityId=` (country from the entity; falls back to the selected or first entity).
- Not done / deferred: automatic application on module enable (no easy hook); "Hajj once" is not enforced (type has no balance); the payroll `LeaveFeed` still counts a half day as a full day (int days; payroll agent's area); pending days in the balance rows are attributed to the start year of a request; no old test changed.
- Tests: `ERP.UnitTests/LeaveEntitlementTests.cs` (9), `ERP.IntegrationTests/LeavePolicyTests.cs` (7). Full solution: unit 388 pass, integration 440 pass (8 skipped before my change).

## Review (Claude)
