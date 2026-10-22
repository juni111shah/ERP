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
| Approvals | Partial | Inbox and decisions exist; approval rules per document type are not configurable yet. |
| Dashboard KPIs | Missing | Needs invoices and receipts first. |
| Finance ledger (journals, posting engine, period lock, trial balance, GL) | Missing | Next sprint. |
| Invoices and billing run from rent schedule | Missing | Depends on the ledger. |
| Receipts, allocation, AR ageing | Missing | |
| Post-dated cheque register | Missing | |
| Bills, payments, AP | Missing | |
| Bank and cash, reconciliation | Missing | |
| HR, payroll, attendance | Missing | |
| Acquisitions and funding | Missing | |
| Reports (16 in the spec) | Missing | Rent roll and occupancy are derivable from existing data. |
| Global search, notification centre | Partial | Notifications API exists; no UI. |
| Tenant portal | Missing | |
| Lease-expiry scheduler | Missing | Settings for alert days exist. |

Planned order: Finance ledger → invoices and billing run → receipts and AR → PDC register → dashboard KPIs → payables → reports → HR → acquisitions.
