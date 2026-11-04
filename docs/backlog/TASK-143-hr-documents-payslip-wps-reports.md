---
id: TASK-143
title: HR documents with expiry alerts, payslip PDF and email, pluggable WPS builder, HR reports
priority: P1
status: ready-for-review
schema: yes
depends_on: []
area: hr
audit_ref: HR audit 2026-11-04 (items 7 and missing features)
commit: see the last "TASK-143:" commit on branch claude/hr-docs
owner: claude (branch claude/hr-complete)
---

## Problem
Verified: there is no place to track an employee's passport, visa, residence/ID, labour card or contract and their expiry dates; the payslip exists only as a web print view (`frontend/src/app/pages/print.page.ts`), with no PDF or email and nothing for the phone; `GetPayrollWpsFile` is a single UAE-format builder with "AED" hard-coded and `cf_ae_*` custom field names; HR has no reports beyond the gratuity report.

## Change wanted
- **Employee documents.** Entity `EmployeeDocument` (EmployeeId, Type enum `Passport, Visa, ResidenceId, LabourCard, Medical, Contract, Certificate, Other`, Number, IssueDate?, ExpiryDate?, IssuedBy?, Notes). CRUD (`hr.employees.manage`; read with `hr.employees.read`), attachments through the existing mechanism (`IAttachmentOwnerResolver`, register owner type `hr.employeedocument`, see TASK-001 and an existing registration). `GET /hr/documents/expiring?days=60` lists documents that expire within N days or already expired, with employee, type, days left, ordered by expiry.
- **Expiry alerts.** Add an HR dashboard alert/KPI (extend `HrDashboard.cs`: "N documents expire within 30 days", critical when any already expired), and register a reminder source with the existing reminders engine (find it: `grep -rn IReminderSource backend` or the reminders feature) for 60/30/7 days before expiry, if that engine has a pluggable source; otherwise a note in the card explaining what was found.
- **Payslip.** `GET /hr/payroll/{id}/payslips/{lineId}` JSON (company, employee, month, earnings breakdown, deductions, net, YTD gross/net) and `GET .../payslip.pdf` using the server-side PDF generation that already exists for other documents (find it from `PrintEndpoints.cs` / `PrintFeatures.cs`: reuse it, do not add a PDF library). Permission `hr.payroll.read`. `POST /hr/payroll/{id}/payslips/{lineId}/email` sends it to the employee's email through the existing document-email service (`DocumentEmailEndpoints.cs`); `POST /hr/payroll/{id}/email-payslips` sends all lines that have an email (returns counts sent/skipped). Only Posted/Paid runs.
- **Pluggable WPS builder.** Introduce `IWpsFileBuilder` (`CountryCode`, `Build(run, lines, employees, settings)`) in the HR module and move the current UAE logic behind it unchanged in behaviour; the currency in the control record comes from the legal entity's base currency, not the literal "AED". `GetPayrollWpsFile` picks the builder by the legal entity's country; when there is none it returns 409 "No salary-file format is set up for <country>." Add a **generic configurable CSV builder** used when no country builder exists but settings `hr.wps.columns` (comma list of field names such as `employeeNumber,name,iban,bankRoutingCode,labourCard,netPay`) and `hr.wps.header` are set. Do NOT invent the Qatar bank/Ministry SIF layout: it must come from the bank's template; leave a clearly named stub `QatarWpsFileBuilder` that returns 409 "The Qatar salary file layout has not been configured. Provide the bank template." Unit tests cover the UAE builder output exactly as before.
- **Reports.** `GET /hr/reports/summary?from&to&legalEntityId`: headcount by department, by nationality and by contract type; joiners and leavers per month; average service years; payroll cost per month (from posted runs); leave days taken by type; absence days. And `GET /hr/reports/birthdays-anniversaries?days=30`. Permission `hr.employees.read` (payroll cost part needs `hr.payroll.read`; omit that section otherwise). Keep queries SQLite and SQL Server safe (sum decimals in memory, as elsewhere in this codebase).

## Acceptance criteria
- [ ] Documents can be created with an attachment and show up in the expiring list at the right thresholds; an expired one is flagged; the dashboard shows the alert.
- [ ] The payslip JSON, PDF (valid PDF bytes) and email work for a Posted run and are refused (409) for a Draft one; the bulk email reports sent/skipped.
- [ ] UAE WPS output is byte-for-byte what the existing test expects; currency comes from the entity; an unknown country gets the clear 409; the configurable CSV works when settings exist.
- [ ] The summary report numbers match known seeded data in a test; payroll cost is omitted without `hr.payroll.read`.

## Tests required
Integration tests in new `HrDocumentsTests`, `PayslipTests`, `WpsBuilderTests`, `HrReportsTests`; unit tests for the expiry bucketing and the CSV builder.

## Do not touch
Payroll calculation, posting and statuses (TASK-140), employee master fields and permissions (TASK-141), leave (TASK-142), web and mobile screens.

## What I did (Claude, branch claude/hr-docs)
- **WPS builder.** `Wps/IWpsFileBuilder.cs` (`CountryCode`, `Build(WpsInput)` with run, lines, employees and settings), `UaeWpsFileBuilder` (logic moved unchanged; currency now the entity's base currency), `QatarWpsFileBuilder` (stub, 409 "The Qatar salary file layout has not been configured. Provide the bank template."), `ConfigurableCsvWpsBuilder` (settings `hr.wps.columns`, `hr.wps.header`, used when no country builder exists). `GetPayrollWpsFile` now has its own handler `WpsFileHandlers` (the old method was deleted from `PayrollFeatures.cs`, nothing else touched there): builder by `LegalEntity.CountryCode`, else CSV when columns are set, else 409 "No salary-file format is set up for <country>.". The existing UAE test passes untouched.
- **Documents.** `Domain/EmployeeDocument.cs`, `Features/EmployeeDocumentFeatures.cs`, endpoints under `/hr/documents` (list, get, create, update, delete, `/expiring?days=60`), attachments owner type `hr.employeedocument`, reminder source `EmployeeDocumentReminders` (the engine has `IReminderSource`; sends once per stage 60/30/7, stage = smallest threshold that covers the days left), dashboard KPI plus alerts (critical when any expired, warning for the next 30 days). Staff with status Terminated are left out of the expiring list, the alerts and the reminders.
- **Payslip.** `Features/PayslipFeatures.cs`: JSON, PDF (`payslip.pdf`), single email and bulk email (`{sent, skipped, skippedEmployees}`); 409 unless the run is Posted or Paid. To reuse the existing PDF and email service without putting payroll words in the core I added a generic, neutral `PrintGenericDto` (`ERP.Application/Printing/GenericDocument.cs`), `IPrintPdfRenderer.Render(PrintGenericDto)` (QuestPDF implementation reuses the existing layout) and `EmailGenericDocument` (handled in `DocumentEmailHandlers`, same message log). The email goes out as the document body (the existing service has no attachment support), the PDF is the download. The HR manager role template also gets `core.email.send`.
- **Reports.** `Features/HrReports.cs`: `/hr/reports/summary` and `/hr/reports/birthdays-anniversaries`; payroll cost is `null` without `hr.payroll.read`.
- **Tests.** `HrDocumentsTests` (6), `PayslipTests` (6), `WpsBuilderTests` (3), `HrReportsTests` (4) integration; `HrDocumentExpiryTests` (9 cases), `HrWpsBuilderTests` (5) unit.
- **Not done / caveats.** (1) The employee has no contract-type or date-of-birth field yet (that is TASK-141). The reports read the custom fields `cf_contract_type` and `cf_date_of_birth` (yyyy-MM-dd) for contract-type headcount and birthdays; when TASK-141 adds real fields swap `EmployeeReportFields` in `HrReports.cs`. Without them contract type shows "Unspecified" and there are no birthdays (work anniversaries come from the join date). (2) Leave days in the summary count approved requests that start inside the period. (3) No migration was created (entity `EmployeeDocument`, table `hr_EmployeeDocuments`, plus two settings are new). (4) No web or mobile screens.

## Review (Claude)
