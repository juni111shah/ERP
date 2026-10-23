# Gap analysis: Real Estate build spec vs. the repository

Status of each area of the Real Estate build specification against `main`. Updated whenever a sprint merges.
Legend: **Done** = backend + UI + tests merged · **Partial** = some of it exists · **Missing** = not started.

| Area | Status | Notes |
|---|---|---|
| Multi-tenant org, legal entities, business units | Done | CRUD + custom fields; admin screens. |
| Users, roles, permissions, audit log | Done | Invite, role assignment, last-administrator guard, permission checklist. |
| Custom fields on every entity | Done | Generic definitions, list filters, form rendering. |
| Country packs (tax codes, country fields) | Done | AE, SA, PK, IN, GB, US; neutral fallback for other countries. |
| Onboarding (country, currency, time zone) | Done | Countries and currencies come from the API. |
| Parties, items, chart of accounts, tax codes | Done | List, create, edit screens. |
| Properties, units, unit types | Done | Occupancy and yield summary. |
| Leases, rent schedule, renewal, termination | Done | Occupying-lease uniqueness enforced by the database. |
| Maintenance work orders | Done | |
| Brokerage deals and commissions | Done | |
| Approvals | Done | Inbox, decisions, and rules (document type + amount) that gate bill posting. |
| Dashboard KPIs, charts, alerts | Done | Core and module contributors; cheque and lease alert windows are settings. |
| Finance ledger (journals, posting engine, period lock, trial balance, GL, P&L, balance sheet) | Done | `IPostingService` is the only way documents write to the ledger. |
| Invoices, credit notes and rent billing run | Done | Billing run invoices due instalments; voiding releases them. |
| Receipts, allocation, AR ageing, customer statement | Done | Cheque receipts come with the PDC register. |
| Post-dated cheque register | Done | Custody, deposit, clear, bounce, replace; maturity KPIs. |
| Bills, payments, issued cheques, AP ageing | Done | Approval rules per document type (bills) are configurable. |
| Bank and cash, reconciliation | Done | Statement import (paste), auto-match, book unmatched lines, transfers, cash position. |
| HR, payroll, attendance, gratuity | Done | Separate `hr` module: employees, monthly attendance exceptions, payroll accrual and payment, bank file, end-of-service provision under the country rule (UAE pack). |
| Acquisitions and funding | Done | Draft, complete (capitalise), annuity and 0% schedules, pay instalments, funding overview, forecast source. |
| Reports (16 in the spec) | Done | 16 built: P&L, balance sheet, trial balance, GL, tax summary, AR/AP ageing, rent roll, occupancy, portfolio performance, lease expiries, cash movements, cash forecast, maintenance costs, commissions. Gratuity report is in the HR module. CSV and Print on each. |
| Global search, notification centre, audit diff | Done | `GET /search` fans out to `ISearchProvider`s (core, real estate, HR; permission and module aware); header search box, notification drawer with mark-all-read, audit before/after drawer. |
| Reminders and communication log | Done | `IReminderSource` (core: bills due, cheques maturing; RE: lease ending, rent overdue incl. tenant email), pluggable `IMessageChannel` (in-app, email outbox), idempotent nightly job, message log screen. |
| Tenant portal | Done | Email one-time-code sign-in (`/portal`), portal-only JWT (no staff permissions), own invoices, receipts, statement; real estate adds lease with rent schedule and maintenance requests (become work orders). Isolation tested. |
| Lease-expiry scheduler | Done | Generic `IScheduledJob` runner (per workspace, per enabled module); the RE job ends expired leases. |
| Security deposits, termination credit notes, brokerage commission invoicing and payout | Done | Deposit register with settle/rollover, credit notes for rent beyond termination, deal Invoiced/Collected/Agent paid driven by finance. |
| Lease renewals workbench | Done | Expiring leases with state (due / draft / renewed), rent vs asking, bulk draft renewals with rent change. |
| Preventive maintenance | Done | Recurring schedules raise work orders ahead of the due date (nightly job or run now). |
| Budgets vs actuals | Done | Annual budget per income/expense account and month; budget-vs-actual report against the posted ledger (favourable/unfavourable variance); monthly grid editor. |
| Inspections | Done | Move-in/move-out/routine checklists, damage cost total, completion raises repair work orders. |
| Owner statements and management fee | Done | Managed-for-owner properties carry a fee %; the owner statement nets collected rent, fee and completed maintenance (cash basis). Report with CSV/Print. |
| Document vault with expiry | Done | Attachments carry category, notes and expiry; vault screen sorted by expiry (expired/expiring highlighted); upload/download on leases; expiry reminders to the message log. |
| Two-factor sign-in (TOTP) | Done | Authenticator-app setup, confirmation, 8 one-time recovery codes, enforced at login, turn-off needs password and code. Account security screen. |
| Data import wizards | Done | Generic CSV import (`IImportDefinition`): parties, items, properties, units. Template download, dry run in a rolled-back transaction, row-level error report. Modules add their own definitions. |
| Inventory | Done | Warehouses, receipts (post Dr Inventory), issues at moving-average cost (Dr COGS), adjustments, same-entity transfers, reorder levels + low-stock alert, stock on hand/valuation, movement history, dashboard card. |
| CRM: listings and leads | Done | Listings (draft/published/closed), leads pipeline with stages and lost reason, convert to brokerage deal, follow-up reminders, lead search. |
| Vendor portal | Done | Vendor email-code sign-in, own bills, payments, statement and assigned work orders with progress updates. |
| Arabic and RTL | Done | Language switcher, `dir`/`lang`, Arabic dictionary for shell, login, dashboard, portals and account screens; other screens fall back to English. |

Planned order: Finance ledger → invoices and billing run → receipts and AR → PDC register → dashboard KPIs → payables → reports → HR → acquisitions.
