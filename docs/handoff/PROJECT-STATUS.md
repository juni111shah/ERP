---
title: "ERP: Project Status and Handoff"
subtitle: "Repository juni111shah/ERP"
date: "Last updated: 2026-11-06 (Asia/Karachi, UTC+5). Verified main HEAD: c319448 (c3194481a9370a58da89c39bf112a271d74833ed), merge of PR #106"
---

# 0. Read this first

This document lets an AI coding agent (Claude Code, Antigravity, Cursor, Codex or similar) with **no prior context** pick up any open task in this repository. It covers what the product is, the hard rules, the state of every module, what has been merged, what is pending, paused, or unverified, known gotchas, and the workflow for shipping a change.

- **Owner / approver:** Muhammad Junaid (repo owner `juni111shah`).
- **Coordinator:** Grok Bot (merges patches from the specialist agents through PRs).
- **Specialist agents:** DB Expert, Full Stack Developer Expert, Advanced UI ux expert, ui ux Expert, Business analyst, dev ops, SQA, Product Mind.
- **Also read:** `AGENTS.md` (coding rules for every AI tool), `CLAUDE.md`, `docs/ARCHITECTURE.md`, `docs/CONTRIBUTING.md`, `docs/devops/*.md`, `docs/team/*` (specs).
- **Keep this file current** after every completed task or merge (same PR or a follow-up docs PR).
- Facts that could not be confirmed from the repo, GitHub, or Git history are marked **(unverified)**. Do not treat older reviews (`docs/PROJECT-REVIEW-2026-11-05.md`, `docs/PROJECTS-REVIEW-2026-11-05.md`, `docs/backlog/STATE.md`) as current; they were snapshots and parts of them were superseded the same day or by later merges.

**Verified at write time (2026-11-06):**

- `origin/main` HEAD = **c319448** (PR **#106**). No commits after #106 on `main`.
- Open PRs against the repo: **none** (`gh pr list --state open` returned empty).
- GitHub Actions workflow **CI** (`ci.yml`) on `main` at c319448: **success**. That is the build/test workflow, not the paused deploy pipeline.
- This file describes **code on `main`**. It does **not** describe Muhammad's local `ERP_Dev` database. A country pack or seeder in the repo is not evidence that a conversion was applied on the owner's PC.

# 1. What this is

A **generic, multi-tenant, multi-industry ERP**. The core (organizations/workspaces, legal entities, business units, users/roles/permissions, parties, items, taxes, currencies, chart of accounts, journals, invoices, bills, receipts, payments, cheques, approvals, attachments, audit, notifications, settings, custom fields) is industry- and country-neutral. Industries plug in as **modules**; country rules ship as **country packs**. The public product name used in the mobile/web brand is **HigherSquare 360**.

## 1.1 Stack

| Layer | Technology |
|---|---|
| Backend | .NET 8 (SDK pinned in `backend/global.json`: 8.0.100, rollForward latestFeature), ASP.NET Core **minimal APIs**, **MediatR** CQRS + FluentValidation, **EF Core 8**, Serilog, SignalR |
| Database | **SQL Server only** (never SQLite/Postgres/MySQL for the app). Dev DB `ERP_Dev` on Muhammad's local SQL Server (Windows auth) |
| Architecture | Clean architecture: `ERP.Domain` -> `ERP.Application` -> `ERP.Infrastructure` -> `ERP.Api`; `ERP.Modularity` holds `IModule` + registry |
| API | Every endpoint returns the `ApiResponse<T>` envelope `{ success, statusCode, message, data, errors, traceId }` (RFC 7807 compatible errors). Core routes `/api/v1/...`; module routes `/api/v1/m/{moduleKey}/...` |
| Frontend | **Angular 19 standalone** components + **ng-zorro-antd 19.3.1**, theme CSS variables `--erp-*`, EN/AR i18n with RTL |
| Mobile | Flutter app in `mobile/` (`erp_mobile`): Riverpod, go_router, Dio; branded HigherSquare 360 |

## 1.2 Solution structure

```
backend/
  ERP.sln, Directory.Build.props, Directory.Packages.props, global.json
  src/ERP.Domain            entities, enums, domain rules (no dependencies)
  src/ERP.Application       MediatR commands/queries + handlers, validators, abstractions
                            (areas: Assistant, Audit, Auth, Catalog, Common, Customization,
                             Dashboard, Finance, Imports, Organizations, Parties, Platform,
                             Portal, Printing, Reminders, Search, Security, Settings,
                             Templates, Workflow)
  src/ERP.Infrastructure    EF Core (AppDbContext), migrations, repositories, services
  src/ERP.Api               minimal-API endpoints (Endpoints/*.cs), Program.cs, appsettings.*
  src/ERP.Modularity        IModule contract and module registry
  modules/                  Assistant, Crm, Distribution, Hr, Inventory, Manufacturing,
                            Pos, Projects, Purchasing, RealEstate, Retail, Sales, Services
  countrypacks/             AE, GB, IN, PK, QA, SA, US
  tests/ERP.UnitTests | ERP.IntegrationTests
frontend/
  src/app/core      api, auth, custom-fields, guards, http, i18n, services, session
  src/app/layout    shell, global-search, notification-drawer, workspace-assistant
  src/app/pages     dashboard, parties, print, reports, portal, pricing, register,
                    admin/*, crm/, distribution/, manufacturing/, platform/, pos/,
                    projects/, purchasing/, retail/, sales/, services/
  src/app/modules   realestate/*, inventory/*, hr/*
  src/app/shared    paged-table, pagination-bar, expand-search, reveal.directive,
                    record-drawer, detail-page, table-tools, stat-card, party-statement,
                    brand-assistant, create-project-button, ...
mobile/             Flutter app (features: auth, dashboard, home, hr, intro, more,
                    notifications, onboarding, ops, payables, projects, purchasing,
                    realestate, receivables, sales, crm, settings)
database/scripts    SQL scripts matching migrations (0001-0027, then 0065-0084)
deploy/             api.Dockerfile, docker-compose.yml, windows/*.ps1, cloudflare/
tools/              qa-smoke.mjs, seed-demo.mjs, smoke-i18n.mjs, smoke-rtl.mjs,
                    smoke-stat-cards.mjs, smoke-history.mjs, assistant-eval/
docs/               ARCHITECTURE, CONTRIBUTING, GAP-ANALYSIS, ROADMAP, devops/,
                    team/, handoff/, backlog/, PROJECT-REVIEW-2026-11-05.md,
                    PROJECTS-REVIEW-2026-11-05.md
```

Host registration (`ERP.Api/Program.cs` `ModuleCatalog.All()`), in this order: `realestate`, `inventory`, `hr`, `projects`, `sales`, `purchasing`, `pos`, `retail`, `distribution`, `manufacturing`, `crm`, `services`, `assistant`. Planned (not built) modules in `ModuleRegistry.Planned`: `fleet`, `hospitality`, `maintenance`.

## 1.3 Run locally (Muhammad's Windows PC)

1. **Database:** local SQL Server, database `ERP_Dev`, Windows authentication (`appsettings.Development.json`). In Development the API creates `ERP_Dev` if missing, applies EF migrations and runs the dev seed at startup. **Whether the owner's current `ERP_Dev` has every migration and the Qatar demo conversion is unverified from this repo.**
2. **API:** Visual Studio 2022, launch profile **`ERP.Api`** -> `http://localhost:5080` (or `dotnet run --launch-profile ERP.Api` in `backend/src/ERP.Api`).
3. **Web:** `cd frontend; npx ng serve --proxy-config proxy.conf.json` -> `http://localhost:4200`. `proxy.conf.json` forwards `/api` and `/hubs` to 5080.
4. **Demo data:** `node tools/seed-demo.mjs` once the API is up (safe to repeat). Dev sign-ins are listed in the root `README.md` (dev-only demo credentials; do not reuse anywhere else). Seeders and demo content are Qatar/QAR-flavoured; that is sample data, not a product identity.
5. Linux/macOS alternative: `./run-dev.sh` with your own connection string via user-secrets or `ConnectionStrings__Default`.
6. **Tunnels:** a Cloudflare **quick tunnel** (`*.trycloudflare.com`, URL changes on restart) may expose the dev app on 4200. A **named tunnel** setup (permanent hostnames) is merged in `deploy/cloudflare/` (PR #58) but **paused by the owner** — see section 5.1. Do not start that work unless the owner says so.

# 2. Hard rules (non-negotiable)

1. **The core stays generic.** Industry concepts live in modules (`backend/modules/*`, `frontend/src/app/modules/*` and module page folders); country rules live in country packs (`backend/countrypacks/*`). The vocabulary denylist test `backend/tests/ERP.UnitTests/core-vocabulary-denylist.txt` enforces this for core code. Do not tie the product to one business type. Never use the brand names of the reference/demo system anywhere (code, comments, docs, commits, PR text, file names). **HigherSquare 360** is the allowed product name.
2. **SQL Server only.**
3. **No secrets** in the repo, PRs or docs (no passwords, tokens, connection strings with credentials, tunnel credentials).
4. **`main` is protected:** every change goes through a PR, merged with **squash merge**. **Never force-push**, never push directly to `main`, never delete protected branches.
5. **`git pull` (or `git fetch` + branch from `origin/main`) before starting work.** If you need latest `main` later, **merge** it in; do not rebase or force-push.
6. **No database data or schema changes** (migrations, SQL scripts, data fixes against `ERP_Dev` or any shared DB) **without the owner's explicit approval.** Back up first.
7. **Remove no existing functionality.** Changes are additive; existing callers keep working.
8. **Do not commit local edits to `frontend/angular.json` or `frontend/package-lock.json`.**
9. **UI standards:**
    - Search fields: icon-only `erp-expand-search` (`shared/expand-search.component.ts`) that expands on hover or click.
    - Table action/toolbar buttons: icon only, label revealed on hover via the `erpReveal` directive (`shared/reveal.directive.ts`).
    - Tables: the shared `erp-pagination-bar` (`shared/pagination-bar.component.ts`) with an always-visible horizontal scrollbar; shared `paged-table`.
    - Loading: ng-zorro loaders/skeletons (no blank panels or charts while loading).
    - i18n: every user-visible string has EN and AR entries in `frontend/src/app/core/i18n/dictionaries.ts` (phrases in `phrases.ar.ts`); layouts must work in RTL.
10. **Only manually entered journals can be reversed.** System-posted journals (from invoices, bills, receipts, payments, module postings) must be undone at their source document (PR #44; reverse returns 409 for source journals).
11. From `AGENTS.md`: endpoint -> one MediatR request -> repository -> DB; nothing outside Infrastructure touches `DbContext`; generic paging/search/sort (`PagedQuery`, `PagedResult<T>`, `ApplyPaging/ApplySearch/ApplySort/ApplyFilters`); every business entity is org-scoped; permissions `<module>.<resource>.<action>` lowercase with `.read`; soft delete for master data; ETag/If-Match concurrency.

# 3. Module status

Status legend:

- **Complete** = built and squash-merged to `main`; CI build/test workflow has been green on recent `main` SHAs. Not a claim that every gap is closed or that the owner's PC was click-tested.
- **Partial** = in `main` and usable in principle, with known gaps or unverified depth.
- **Pending** = not built.
- **Paused by owner** = code may exist; **do not start this** until Muhammad says otherwise.

| Area | Status | Key files | Notes |
|---|---|---|---|
| Platform core (tenancy/workspaces, auth, refresh rotation, lockout, reset, onboarding with industry templates) | Complete | `ERP.Application/Auth`, `Organizations`, `Templates/Templates.cs`, `OnboardingService` | UI calls an organization a "Workspace" |
| Founder console, public registration, plans, platform billing | Partial | `ERP.Application/Platform/*`, `ERP.Api/Endpoints/PlatformAdminEndpoints.cs`, `pages/platform/*`, `pages/register*.ts`, `pages/pricing.page.ts` | Merged #104/#106. Founder console is `/api/v1/platform-admin/*` (`platform-admin` policy). Public signup, legal docs, plans, clients, invoices, activity, team. **Live billing/ops unverified.** |
| Users, roles, permissions | Complete | `ERP.Application/Security/Permissions.cs`, `pages/admin/users.page.ts`, `roles.page.ts` | |
| First-sign-in two-factor (TOTP) | Complete | `ERP.Application/Auth`, `pages/two-factor-step.component.ts`, mobile `features/auth` | PR #94. `Auth:TwoFactor:Enforce` default **true** in `appsettings.json`, **false** in Development. Staff portal only; tenant portal sign-in unchanged |
| Module system (per-workspace enable/disable) | Complete | `ERP.Modularity`, `pages/modules.page.ts` | 13 installed modules. Disabled module returns 403 `module-disabled`. Planned placeholders: fleet, hospitality, maintenance |
| Parties (customers/vendors/owners) + statement tabs + party groups | Complete | `ERP.Application/Parties`, `pages/parties.page.ts`, `party-detail.page.ts`, `pages/admin/party-groups.page.ts` | Party groups + ageing by group (#78) |
| Items, tax codes, currencies, legal entities, business units | Complete | `pages/admin/items\|tax-codes\|legal-entities\|business-units.page.ts` | TRN and trade licence on legal entities (#48) |
| Chart of accounts | Partial | `pages/admin/accounts.page.ts`, `pages/admin/coa/*`, `AccountNumbering.cs` | Hierarchical numbering Option A (#60). Option B still backlog. Demo CoA often flat **(unverified on ERP_Dev)** |
| General ledger: journals, posting, reverse | Complete | `JournalFeatures.cs`, `Posting.cs`, `pages/admin/journals.page.ts` | Manual-only reversal (#44) |
| AR: invoices, credit notes, receipts | Complete | `InvoiceFeatures.cs`, `ReceiptFeatures.cs`, `pages/admin/invoices.page.ts` | Void with reason; credit note from invoice |
| AP: bills, payments | Complete | `BillFeatures.cs`, `PaymentFeatures.cs`, `pages/admin/bills.page.ts`, `payments.page.ts` | P2P input/guards in #81 |
| Cheques / PDC, bank | Complete | `ChequeFeatures.cs`, `pages/admin/cheques.page.ts`, `bank.page.ts` | Bounce follow-up (#77) |
| Printing + server PDF | Complete | `ERP.Application/Printing`, `pages/print.page.ts` | JSON print + `GET .../pdf` (QuestPDF, #92). Live printer/SMTP unverified |
| Budgets, approvals, audit, attachments, documents, messages, imports | Complete (unverified depth) | `pages/admin/budgets\|approvals\|approval-rules\|audit\|documents\|messages\|import.page.ts` | SMTP optional, off by default; document emailing since #71; never tried against a live SMTP server |
| Custom fields | Complete | `ERP.Application/Customization`, `pages/custom-fields.page.ts` | JSON column per extensible table |
| Reports | Partial | `pages/reports/report-registry.ts`, `pages/admin/ledger-reports.page.ts` | Trial balance, P&L, BS, ageing, tax summary, module reports. Cash-flow statement not found. E-invoicing not built |
| Overview dashboard | Partial | `pages/dashboard.page.ts` | Date range #86/#87. Unification of stat cards #62 |
| Shared tables / UX standards | Complete | `shared/paged-table`, `pagination-bar`, `expand-search`, `reveal.directive`, `stat-card` | Header global search is expand-search (#61) |
| i18n EN/AR + RTL | Partial | `core/i18n/dictionaries.ts`, `phrases.ar.ts`, `tools/smoke-i18n.mjs`, `smoke-rtl.mjs` | Runtime EN-string map; new English can silently stay English. Native-speaker Arabic review **(unverified / not done)** |
| Real Estate module | Partial | `backend/modules/ERP.Modules.RealEstate/*`, `frontend/src/app/modules/realestate/*` | Deep: properties, units, leases, billing, deposits, WO, PM, inspections, renewals, listings, leads, deals, acquisitions, owner statements, tenant portal. Country registry integrations not built |
| Inventory module | Partial | `backend/modules/ERP.Modules.Inventory/*`, `frontend/src/app/modules/inventory/*` | Warehouses, stock, movements, counts, valuation reports. No lot/serial/bin **(no code found)** |
| HR module | Partial | `backend/modules/ERP.Modules.Hr/*`, `frontend/src/app/modules/hr/*` | Employees, leave, payroll, reviews, loans, termination, statutory rules, daily attendance, shifts, roster, punch API, recruitment, checklists, documents, payslip, UAE WPS. **Not done:** device photo/face capture, vendor device connectors, Qatar WPS file layout (builder throws until a bank template is provided) |
| Sales module | Partial | `ERP.Modules.Sales`, `pages/sales/*` | Quotes, orders, order-to-invoice. Breadth-first; delivery notes / RMA not found |
| Purchasing module | Partial | `ERP.Modules.Purchasing`, `pages/purchasing/*` | Requisitions, POs, goods receipts, GR/IR, PO-to-bill. P2P rules #81. RFQ / landed cost not found |
| POS module | Partial | `ERP.Modules.Pos`, `pages/pos/*`, mobile `ops/pos_*.dart` | Registers, sessions, till, park/held, returns, shift reports (#104). Mobile till brought toward web (#105). Offline queue not found |
| Retail module | Partial | `ERP.Modules.Retail`, `pages/retail/*` | Stores, price lists, promotions. Depth unverified; little/no mobile **(Nov 2026 review; later mobile POS is separate)** |
| Distribution module | Partial | `ERP.Modules.Distribution`, `pages/distribution/*` | Routes, van trips, wholesale pricing. Mobile distribution screens not found in a dedicated folder |
| Manufacturing module | Partial | `ERP.Modules.Manufacturing`, `pages/manufacturing/*` | BOMs, work orders, costing. No MRP/routing **(no code found)** |
| CRM module (standalone) | Partial | `ERP.Modules.Crm`, `pages/crm/*` | Leads, opportunities, interactions. Real-estate listings/leads still live under the RE module |
| Projects module | Partial | `ERP.Modules.Projects`, `pages/projects/*`, mobile `features/projects` | Large waves #102/#103: board, week, timer, expenses, approvals, reports, resources, change orders, risks, plan, billing preview, WIP, portal sign-off (drawn + hash), members-only lists, create-from SO/opportunity. Remaining gaps in `docs/PROJECTS-REVIEW-2026-11-05.md` §7 (retention/advances, payroll cost on projects, certified e-sign provider, some mobile screens) |
| Services module | Partial | `ERP.Modules.Services`, `pages/services/*` | Engagements, retainers, milestones, time. Depth unverified |
| AI assistants | Partial | `ERP.Modules.Assistant`, `ERP.Application/Assistant`, `pages/platform/platform-assistant.page.ts`, `layout/workspace-assistant.component.ts`, `shared/brand-assistant.component.ts` | Public/brand assistant + workspace assistant (#106). Founder controls providers (`AiProviders`, brand vs workspace flags, usage caps). **Needs live provider keys; live LLM unverified** |
| Country packs AE, GB, IN, PK, **QA**, SA, US | Partial | `backend/countrypacks/ERP.CountryPacks.*` | Taxes, custom fields, leave/EOS where coded, simplified tax returns (#73 + QA pack). **Qatar pack in repo ≠ ERP_Dev converted.** Qatar WPS is a stub. E-invoicing (ZATCA / UAE / GST) not built |
| Mobile app (Flutter) | Partial | `mobile/` | #88 screens across modules; #89-#91 visual polish; #93 intro tour; #94 2FA; #95 palette/onboarding/touch; #96 HigherSquare 360 brand; #97 intro back nav; #105 POS till. Not full web parity. Dark mode still partial in places |
| CI build/test (`ci.yml`) | Complete (workflow) | `.github/workflows/ci.yml` | Succeeded on `main` at c319448 (verified 2026-11-06) |
| CI/CD deploy (dev/staging/production) | Partial, **Paused by owner** | `.github/workflows/cicd.yml`, `deploy/windows/*`, `docs/devops/*` | Runner, GitHub environments, promotions **not set up**. Do not start |
| Hosting / named tunnel | Partial, **Paused by owner** | `deploy/cloudflare/*`, `PublicOrigin.cs` | Code merged (#58); not run. Do not start |
| Planned modules (fleet, hospitality, maintenance) | Pending | `ModuleRegistry.Planned` in `ERP.Modularity/Modules.cs` | Placeholders in Settings → Modules / onboarding only |

## 3.1 EF Core migrations

**54** migrations in `backend/src/ERP.Infrastructure/Persistence/Migrations` (excluding Designers and the snapshot). Matching scripts live in `database/scripts/` (`0001`–`0027`, then `0065`–`0084`; there are no files numbered 0028–0064).

**Whether any of these are applied on the owner's `ERP_Dev` is unverified.** In Development the API auto-migrates at startup *if it is started against that database*. Do not apply migrations or data conversions without owner approval and a backup.

| # | Migration | # | Migration |
|---|---|---|---|
| 1 | 20261022132212_InitialCore | 28 | 20261029154404_InventoryCounts |
| 2 | 20261022145011_RealEstateModule | 29 | 20261029160406_HrLeave |
| 3 | 20261022191803_LedgerJournalAmount | 30 | 20261029201608_OwnerCostBasis |
| 4 | 20261022192452_Receivables | 31 | 20261029202556_ChequeBounceFollowUp |
| 5 | 20261022193656_ChequesAndBank | 32 | 20261029203606_PartyGroupsAndCodeReuse |
| 6 | 20261022194501_Payables | 33 | 20261029235542_IdempotencyRecords |
| 7 | 20261022200718_RealEstateFinance | 34 | 20261030045924_PosOneOpenSessionPerRegister |
| 8 | 20261022201459_Acquisitions | 35 | 20261030051854_FinanceReportingIndexes |
| 9 | 20261022202642_HrModule | 36 | 20261030114901_InvoiceLineSourceLine |
| 10 | 20261023035937_Reminders | 37 | 20261104110602_HrCompletion |
| 11 | 20261023040602_TenantPortal | 38 | 20261104174839_HrWave1 |
| 12 | 20261023041637_RealEstateOps | 39 | 20261104183934_HrWave4 |
| 13 | 20261023042754_Budgets | 40 | 20261105032751_HrHalfDay |
| 14 | 20261023045610_Inspections | 41 | 20261105034402_HrShifts |
| 15 | 20261023050015_OwnerStatements | 42 | 20261105040727_HrShiftRoster |
| 16 | 20261023050718_DocumentVault | 43 | 20261105095738_ProjectsWave2 |
| 17 | 20261023051929_InventoryStock | 44 | 20261105103353_ProjectsLedger |
| 18 | 20261023052928_Crm | 45 | 20261105104451_ProjectsPortal |
| 19 | 20261023053134_HrExtras | 46 | 20261105105857_ProjectsSignature |
| 20 | 20261023054857_EmailAttempts | 47 | 20261105120054_ProjectsWorkspace |
| 21 | 20261023105111_FinanceAllocationIntegrity | 48 | 20261105175521_PlatformConsole |
| 22 | 20261023105217_FinanceCreditNotes | 49 | 20261105185535_PlatformInvoiceTaxLabel |
| 23 | 20261023105335_FinanceVoidAudit | 50 | 20261105192006_PlatformLegalDocuments |
| 24 | 20261023105511_BillBusinessUnit | 51 | 20261105203822_PosTillCompletion |
| 25 | 20261023105543_FinanceReferentialFks | 52 | 20261106062143_AiAssistantProviders |
| 26 | 20261023105812_FinanceColumnHardening | 53 | 20261106065427_AiAssistantUsage |
| 27 | 20261024071544_ModulesExpansion | 54 | 20261106075512_AiAssistantBrandAndWorkspace |

Scripts 0027 (one open POS session per register) and 0065 (finance reporting indexes) can fail or lock on a live database; apply only with a backup and a maintenance window.

# 4. Completed work log

All PRs below are **squash-merged into `main`** unless noted. Merge SHAs are from `gh pr view` / `git log` on 2026-11-06. PRs **#83, #84, #85** targeted `dev` / `staging` / `production` and are **not** ancestors of `origin/main`.

## 4.1 Pull requests #46 to #78 (already on main before the previous handoff)

Condensed; see git history for bodies.

| PR | Commit | Summary |
|---|---|---|
| [#46](https://github.com/juni111shah/ERP/pull/46) | c6e54b7 | CI/CD docs, named-tunnel plan, optional PublicUrl |
| [#47](https://github.com/juni111shah/ERP/pull/47) | 3c7900b | Shared pagination bar, sticky table scrollbar, hover-reveal |
| [#48](https://github.com/juni111shah/ERP/pull/48) | f8d6871 | SQA round 2: statements, TRN/trade licence, drawer/routes |
| [#49](https://github.com/juni111shah/ERP/pull/49) | 4fdce82 | i18n Arabic pass 2 |
| [#50](https://github.com/juni111shah/ERP/pull/50) | 6e42f65 | Invoice due date from customer terms |
| [#51](https://github.com/juni111shah/ERP/pull/51) | c47cf41 | Dynamic CoA panels |
| [#52](https://github.com/juni111shah/ERP/pull/52) | cd14d41 | Table Refresh, filters, expand-search on tables |
| [#53](https://github.com/juni111shah/ERP/pull/53) | 442941a | Bill due date from vendor terms |
| [#54](https://github.com/juni111shah/ERP/pull/54) | 0e43355 | CoA derived sub-category level |
| [#55](https://github.com/juni111shah/ERP/pull/55) | 7c0fa55 | Arabic strings pass 3 |
| [#56](https://github.com/juni111shah/ERP/pull/56) | 4e23e68 | Lease default end date |
| [#57](https://github.com/juni111shah/ERP/pull/57) | 9c93a63 | CoA leaf details drawer |
| [#58](https://github.com/juni111shah/ERP/pull/58) | 87d9c2f | Optional public URL + Cloudflare named-tunnel **script** (not run; paused) |
| [#60](https://github.com/juni111shah/ERP/pull/60) | db19f90 | Hierarchical CoA numbering Option A + SQA H-1, M-A, M-B (partial) |
| [#61](https://github.com/juni111shah/ERP/pull/61) | 8adb557 | Record history, AR ageing by dimension, expand-search header |
| [#62](https://github.com/juni111shah/ERP/pull/62) | 2f20813 | Unified `erp-stat-card`, skeleton loading |
| [#71](https://github.com/juni111shah/ERP/pull/71) | 78c57e9 | Real document emailing via `IEmailSender` |
| [#72](https://github.com/juni111shah/ERP/pull/72) | 36c572a | Inventory counts and reports. Migration `InventoryCounts` |
| [#73](https://github.com/juni111shah/ERP/pull/73) | 8c3ec67 | Country-pack tax returns (AE, SA, GB, IN, PK, US) |
| [#74](https://github.com/juni111shah/ERP/pull/74) | a8de899 | HR leave. Migration `HrLeave` |
| [#76](https://github.com/juni111shah/ERP/pull/76) | 2e265e8 | Owner cost basis. Migration `OwnerCostBasis` |
| [#77](https://github.com/juni111shah/ERP/pull/77) | ba0c341 | Cheque bounce follow-up. Migration `ChequeBounceFollowUp` |
| [#78](https://github.com/juni111shah/ERP/pull/78) | 2bcf823 | Party groups + **filtered** unique index so soft-deleted account codes can be reused. Migration `PartyGroupsAndCodeReuse` |

Earlier relevant commits: 7fe89dd (#44, manual-only journal reversal), 208acd3 (#45, SQA UI fixes), 7df8788 (void reasons, credit notes, reverse guard), 7a97a05 (schema M1-M6).

## 4.2 Pull requests #79 to #106 (on main since the previous handoff)

The previous `PROJECT-STATUS.md` on `main` still described ~PR #86 / 2026-10-30 and omitted several items that were **already on `main` then** (module expansion #80, Qatar pack / QAR demo seed). This table is the catch-up through current HEAD.

| PR | Commit | Merged (UTC) | Summary |
|---|---|---|---|
| [#79](https://github.com/juni111shah/ERP/pull/79) | bf5e599 | 2026-10-29 | Docs: record PRs #76-#78 |
| [#80](https://github.com/juni111shah/ERP/pull/80) | 4f7c9f3 | 2026-10-30 | **Module expansion:** Sales, Purchasing, POS, Retail, Distribution, Manufacturing, CRM, Projects, Services (+ dashboard work). Large squash (381 files). Migration `ModulesExpansion` |
| [#81](https://github.com/juni111shah/ERP/pull/81) | dcb1ae8 | 2026-10-30 | Procure-to-pay second pass (TASK-131..138): GR vs PO, PO edit/delete guards, PO-to-bill tax/due date, header validation, bill/payment dates and retries |
| [#82](https://github.com/juni111shah/ERP/pull/82) | 26cf65e | 2026-10-30 | POS concurrent-orders test sequential |
| [#86](https://github.com/juni111shah/ERP/pull/86) | ac661c8 | 2026-10-30 | Overview date range `GET /dashboard?from=&to=` |
| [#87](https://github.com/juni111shah/ERP/pull/87) | 2fdef37 | 2026-10-30 | Overview range review: one preset dropdown + Custom picker; max 5 years |
| [#88](https://github.com/juni111shah/ERP/pull/88) | 5c67073 | 2026-11-03 | Mobile screens across finance, RE, HR, CRM, sales, purchasing, ops (inventory/POS/manufacturing), projects |
| [#89](https://github.com/juni111shah/ERP/pull/89) | 017ee8f | 2026-11-03 | Mobile design refresh (theme tokens, kit, notched bottom nav) |
| [#90](https://github.com/juni111shah/ERP/pull/90) | 5595eb3 | 2026-11-03 | Mobile Overview KPI carousel + category chips |
| [#91](https://github.com/juni111shah/ERP/pull/91) | 02a0e29 | 2026-11-03 | Mobile polish v3 (type/spacing, skeletons, motion) |
| [#92](https://github.com/juni111shah/ERP/pull/92) | 1b72e99 | 2026-11-03 | Server-side PDF for invoice/receipt/bill/payment/journal; mobile Print/Share |
| [#93](https://github.com/juni111shah/ERP/pull/93) | 89d135e | 2026-11-03 | Mobile first-open intro tour |
| [#94](https://github.com/juni111shah/ERP/pull/94) | 6533fe2 | 2026-11-03 | First-sign-in TOTP 2FA on web and mobile |
| [#95](https://github.com/juni111shah/ERP/pull/95) | 42888c8 | 2026-11-03 | Mobile command palette, coach tour, plain labels, touch targets |
| [#96](https://github.com/juni111shah/ERP/pull/96) | ba5ce00 | 2026-11-03 | Mobile HigherSquare 360 branding and app icon |
| [#97](https://github.com/juni111shah/ERP/pull/97) | 58c0b3e | 2026-11-03 | Mobile intro tour back navigation |
| [#98](https://github.com/juni111shah/ERP/pull/98) | fded0f2 | 2026-11-04 | HR completion (TASK-140..143): payroll core, people/access, leave policy, documents/payslip/WPS/reports. Migration `HrCompletion` |
| [#99](https://github.com/juni111shah/ERP/pull/99) | 05d93b7 | 2026-11-04 | HR wave 1 screens/docs follow-on |
| [#100](https://github.com/juni111shah/ERP/pull/100) | b572e46 | 2026-11-05 | HR: shifts, device punches, table editors, web component tests |
| [#101](https://github.com/juni111shah/ERP/pull/101) | 6a71f9e | 2026-11-05 | HR dated shift roster. Migration `HrShiftRoster` |
| [#102](https://github.com/juni111shah/ERP/pull/102) | 7566093 | 2026-11-05 | Projects wave + HR leftovers on the same branch: members-only, create from SO/opportunity, drawn signature + evidence hash; backend/web/mobile projects |
| [#103](https://github.com/juni111shah/ERP/pull/103) | fe8b9b2 | 2026-11-05 | Projects repair: unified project page, status-case fixes, timeline/board/week, WIP rules, mobile regroup |
| [#104](https://github.com/juni111shah/ERP/pull/104) | f719190 | 2026-11-06 | Platform console (registration, founder console, billing, legal docs) + POS till completion (returns, held sales, shift reports) + portal/web fixes |
| [#105](https://github.com/juni111shah/ERP/pull/105) | 378c085 | 2026-11-06 | Mobile POS till brought up to the web till; sidebar accordion / collapsed flyouts |
| [#106](https://github.com/juni111shah/ERP/pull/106) | c319448 | 2026-11-06 | Public and workspace AI assistants; founder control of assistant providers/usage. HEAD of `main` |

**Also on `main` before #86, and missing from the old handoff:**

- Qatar country pack (`ERP.CountryPacks.QA`): QAR, `Asia/Qatar`, NOVAT/EXEMPT, QID/CR/tax card fields, Ejar/Kahramaa/municipality custom fields, 21-day EOS, leave templates. Commit trail includes `c330fbb` / merge `7d866fe`.
- Demo seeders and some module defaults use **QAR** and Qatar place names. That is **in-repo sample data**, not proof that `ERP_Dev` on the owner's PC was converted.
- Idempotency records, POS one-open-session-per-register, finance reporting indexes, invoice line source line (migrations 33–36).

## 4.3 Previously documented non-PR items

| Item | Result |
|---|---|
| IsTaxRegistered data fix | Historical: 2 legal-entity rows in `ERP_Dev` **(local DB; not re-verified)** |
| Demo seed | `tools/seed-demo.mjs` exists and is Qatar-flavoured; whether it has been re-run on the owner's PC after the later modules **(unverified)** |
| Product Mind report | Referenced historically at `/workspace/erp-team/product/world-class-report.md`. **`/workspace/erp-team` is not in this clone** |
| Cheque print | In main as 66385aa (instrument details + PDC badge) |
| Soft-deleted account code reuse | **In schema** (#78): filtered unique index `WHERE [IsDeleted] = 0`. Applying that index on `ERP_Dev` **(unverified)** |

# 5. Pending, paused, and unverified

Owner and coordinator: unless stated, the specialist agent prepares a patch or branch and Grok Bot builds, opens the PR and squash-merges.

## 5.1 Paused by owner — do not start

Paused as of **23 Oct 2026** until Muhammad says otherwise. These are **not** the next work to pick up.

### Permanent links (named Cloudflare tunnel)

- **Owner:** Muhammad (only he can do these); dev ops supports.
- **Code:** merged in #58: `deploy/cloudflare/setup-named-tunnel.ps1`, `config.example.yml`, `PublicOrigin.cs` (`ERP_Hosting__PublicUrl`), notes in `docs/devops/cloudflare-named-tunnels.md`. Scripts are untested against real Cloudflare/Windows.
- **Acceptance when unpaused:** `erp-dev.<domain>`, `erp-staging.<domain>`, `erp.<domain>` serve the right apps and survive reboot.

### CI/CD deploy pipeline

- **Owner:** dev ops (prepares), Muhammad (tokens/admin settings).
- **Remaining when unpaused:** fast-forward the `dev` branch to main; register the self-hosted Windows runner (label `erp-deploy`); create GitHub environments and the rulesets in `docs/devops/manual-steps.md`. Workflows: `.github/workflows/ci.yml` (already running), `cicd.yml` (deploy).
- **Acceptance when unpaused:** push to dev/staging deploys automatically; production deploys only commits already on staging; promotions are ff-only, never forced.

## 5.2 Unverified on the owner's PC (cannot be confirmed from the repo)

| Item | What the repo shows | What is unverified |
|---|---|---|
| Latest EF migrations on `ERP_Dev` | 54 migrations in source; API auto-migrates in Development | Whether Muhammad has started the API against `ERP_Dev` since #106 |
| **Qatar / QAR data conversion of `ERP_Dev`** | QA country pack, QAR seeders, Qatar-flavoured demo scripts | Whether existing `ERP_Dev` rows were converted (currency, tax codes, addresses, legal-entity country). **Treat as unverified, not done** |
| Live SMTP | `IEmailSender` + document email (#71) | Never confirmed against a live SMTP server |
| Live LLM providers | Assistant provider table + runner | No evidence of production keys or a successful live chat |
| CoA tree on `ERP_Dev` | Numbering Option A in code; seed often uses flat 4-digit codes | Whether the owner's chart still has `ParentId = NULL` on all accounts |
| finance-check-2 SQA leftovers | Backend in 7df8788 | Whether SQA M-1/M-2/M-3/L-1 are closed in the UI **(unverified)** |

## 5.3 Genuinely open leftover work

These are still open in the repo or explicitly unfinished. They are candidates for a later task; they are **not** paused.

1. **TASK-139** (`docs/backlog/TASK-139-demo-data-all-statuses-three-years.md`, status `open`): demo data does not cover every document status across three years. Seed is Qatar/QAR flavoured but coverage is incomplete. Needs owner-safe seed work only (no production behaviour change).
2. **HR device capture / vendor connectors** (TASK-146 "Not done"): punch API exists (`POST /hr/attendance/punch`); no photo/face capture and no brand-specific device protocols.
3. **Qatar WPS file layout:** `QatarWpsFileBuilder` throws `ConflictException` until a bank template is configured. UAE WPS SIF exists.
4. **Planned modules:** `fleet`, `hospitality`, `maintenance` — not built.
5. **Hierarchical CoA numbering Option B:** real header accounts + renumbering existing codes. Changes existing data (needs owner approval and backup).
6. **pm-schema leftovers (from the old proposal, still no migrations in this repo):** CoreFx, CorePenaltiesDunning, CoreReasonsAuditInstruments, CoreCustomerDeposits, CoreInstalmentPlans, RealEstatePm (`GraceDays` → `RentFreeDays`), deposit backfill. The proposal file lived under `/workspace/erp-team/db/` which is **not in this clone**. Party groups and account-code reuse **are** done (#78).
7. **Opening balances / migration imports:** CSV import exists for parties, items, properties, units. No import found for opening trial balance, open invoices/bills, stock on hand, employees, leave balances.
8. **SQA write tests** (Playwright under `/workspace/erp-team/sqa/playwright`): on hold; that tree is not in this clone.
9. **Backlog card hygiene:** `docs/backlog/STATE.md` last Claude run **2026-10-30** (stale). Cards TASK-131..138 and TASK-140..146 still say `ready-for-review` even though the implementations were merged (#81, #98–#102). Do not treat those YAML statuses as "not in main". A docs-only card update is leftover, not product work.
10. **JS voice-assistant proposals:** skipped by the owner; not part of this project.

## 5.4 Known product gaps (not assigned work)

From code search plus `docs/PROJECT-REVIEW-2026-11-05.md` / `docs/PROJECTS-REVIEW-2026-11-05.md`. Many review items were **fixed later the same day** (especially Projects). Treat the reviews as history; this list is only gaps still visible in source or explicitly marked "not done" after those fixes.

- Finance: no FX revaluation, fixed-asset register, general recurring invoices, AP debit notes, extra analytic dimensions, inter-company, bank feeds, cash-flow statement, supplier payment runs, escalating dunning **(no code found)**.
- Sales: no delivery notes/RMA/drop-ship **(no code found)**.
- Inventory: no lot/serial/bin/FIFO option **(no code found)**.
- POS: no offline queue / ESC/POS printer integration **(no code found)**.
- Projects still missing (after #103): retention/advance/recurring project invoices, multi-currency billing, per-line tax, PO/payroll cost on a project, certified e-sign trust provider, some mobile screens (comments/files/schedule/team/sign-off status were skipped in that web repair round; **whether #105 added any of those is unverified — #105 was POS/sidebar**).
- Country statutory packs beyond AE/QA EOS and UAE WPS: SA/IN/PK/GB/US payroll files not built.
- SSO/SAML, outbound webhooks, GDPR export/erasure, field-level encryption of IBAN/passport **(no code found)**.
- Product Mind roadmap phases 0–3: report not in this clone.

Do not implement a gap from this list unless the owner (or a backlog card) asks for it.

## 5.5 Items from the old handoff that are now done in code

| Old item | Now |
|---|---|
| 5.1 Hierarchical CoA Option A | Done (#60) |
| 5.2 Stat card unification | Done (#62) |
| 5.4 BA P1 stories (history, ageing, cheque bounce, owner cost basis, party groups) | Done (#61, #76, #77, #78) |
| Soft-deleted account code reuse (schema) | Done (#78, filtered unique index) |
| Overview date range | Done (#86, #87) |
| Server PDF / first-login 2FA / mobile intro, brand, palette | Done (#91–#97) |
| HR completion waves | Merged (#98–#101) |
| Projects repair + workspace | Merged (#102, #103) |
| Platform console + assistants + POS till | Merged (#104–#106) |
| PRs #93, #97 "open, not merged" in the old file | Merged |

# 6. Known issues and gotchas

- **`ERP_Dev` is local.** Never assume the owner's database matches the 54 migrations or the Qatar seed. Auto-migrate happens only when the Development API starts.
- **Qatar pack ≠ converted database.** `ERP.CountryPacks.QA` and QAR seeders are in git. Local conversion of an existing `ERP_Dev` is **unverified**.
- **Demo defaults to QAR / Qatar places** in seeders and some module model defaults. The product remains generic; do not spread country-specific concepts into core beyond existing seed/demo helpers.
- **CoA data may still be flat** in a long-lived `ERP_Dev` (all `ParentId` null, 4-digit codes). Virtual tree levels (AccountType + derived sub-category, #54) still apply. `ParentId` historically had no FK **(whether that changed later is unverified in this pass)**.
- **Script 0027** fails if a register already has two open sessions; **0065** builds indexes and can lock tables.
- **`ng serve` goes stale after branch switches:** restart it (and hard-reload the browser) after checkout/merge.
- **`tools/qa-smoke.mjs` types into the header global search** (`data-testid="global-search"`).
- **PowerShell 5 on the PC does not support `&&`:** use `;` (and check `$LASTEXITCODE`).
- Journals and budget lines reference accounts by `AccountId`, not code.
- Integration tests that need SQL Server use `ERP_TEST_SQL`; otherwise they run on in-memory SQLite. A few SQL Server race tests are skipped without that env var.
- SMTP off by default. Assistant providers need keys that must never be committed.
- Arabic i18n is largely an English-string map (`phrases.ar.ts`); reworded English can miss Arabic until someone adds a phrase.
- **Do not commit** `frontend/angular.json` or `frontend/package-lock.json` local edits.
- `/workspace/erp-team` is referenced by older notes and is **not present** in this workspace.

# 7. How to pick up a task

1. **Sync:** `git fetch origin` then `git worktree add ../erp-<topic> -b <tool>/<topic> origin/main` (branch prefixes: `claude/`, `antigravity/`, `cursor/`, `codex/`, `feat/`, `fix/`, `docs/`). Cursor cloud agents use `cursor/<descriptive-name>-<id>`.
2. **Read:** `AGENTS.md`, this file, and the relevant spec in `docs/team/` or a current backlog card. Do not trust `docs/backlog/STATE.md` dates.
3. **Build and test:**
   - `dotnet build backend/ERP.sln` (0 errors)
   - `dotnet test backend/ERP.sln` (unit and integration; SQL Server tests need `ERP_TEST_SQL`)
   - `cd frontend; npm ci; npx ng build` (0 errors; existing unused-pipe warnings are known)
   - Optional: `node tools/qa-smoke.mjs`, `node tools/smoke-i18n.mjs`, `node tools/smoke-rtl.mjs` against a running app.
   - Mobile: `cd mobile && flutter analyze && flutter test` when the change touches Flutter.
4. **Hand-off as a patch (specialist agents):** commit locally, then `git diff --binary origin/main..HEAD > /workspace/erp-team/<agent>/<topic>.patch` if that tree exists; otherwise push a PR.
5. **Merge (coordinator):** push the branch, open a PR into `main` (conventional commit title such as `feat(finance): ...`), wait for CI, **squash-merge**, delete the branch, remove the worktree. Never force-push; never commit `angular.json` / `package-lock.json` edits; never include secrets or the reference system's name.
6. **DB changes:** EF migration + `database/scripts` SQL in the PR; apply to `ERP_Dev` only after owner approval and a backup.
7. **Update this file** (`docs/handoff/PROJECT-STATUS.md`) in the same PR or a follow-up docs PR when a task changes state.

# 8. Last updated

- **2026-11-06** (Asia/Karachi, UTC+5)
- Verified `origin/main` HEAD: **c319448** (`c3194481a9370a58da89c39bf112a271d74833ed`), merge of [PR #106](https://github.com/juni111shah/ERP/pull/106)
- Open PRs at write time: **none**
- GitHub Actions `CI` on that SHA: **success** (deploy/`cicd.yml` still paused by owner)
- Docs-only refresh of this handoff; no application code changed
