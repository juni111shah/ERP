---
title: "ERP: Project Status and Handoff"
subtitle: "Repository juni111shah/ERP"
date: "Last updated: 2026-10-30 19:45 PKT (Asia/Karachi, UTC+5). main at time of writing: ac661c8 (PR #86)"
---

# 0. Read this first

This document lets an AI coding agent (Claude Code, Antigravity, Cursor, Codex or similar) with **no prior context** pick up any open task in this repository. It covers what the product is, the hard rules, the state of every module, what has been merged, what is pending (with owner, next steps, files and acceptance criteria), known gotchas, and the exact workflow for shipping a change.

- **Owner / approver:** Muhammad Junaid (repo owner `juni111shah`).
- **Coordinator:** Grok Bot (merges patches from the specialist agents through PRs).
- **Specialist agents:** DB Expert, Full Stack Developer Expert, Advanced UI ux expert, ui ux Expert, Business analyst, dev ops, SQA, Product Mind.
- **Also read:** `AGENTS.md` (coding rules for every AI tool), `CLAUDE.md`, `docs/ARCHITECTURE.md`, `docs/CONTRIBUTING.md`, `docs/devops/*.md`, `docs/team/*` (specs).
- Facts that could not be confirmed from the repo or reports are marked **(unverified)**.

# 1. What this is

A **generic, multi-tenant, multi-industry ERP**. The core (organizations/workspaces, legal entities, business units, users/roles/permissions, parties, items, taxes, currencies, chart of accounts, journals, invoices, bills, receipts, payments, cheques, approvals, attachments, audit, notifications, settings, custom fields) is industry- and country-neutral. Industries plug in as **modules**; country rules ship as **country packs**.

## 1.1 Stack

| Layer | Technology |
|---|---|
| Backend | .NET 8 (SDK pinned in `backend/global.json`: 8.0.100, rollForward latestFeature), ASP.NET Core **minimal APIs**, **MediatR** CQRS + FluentValidation, **EF Core 8**, Serilog, SignalR |
| Database | **SQL Server only** (never SQLite/Postgres/MySQL for the app). Dev DB `ERP_Dev` on Muhammad's local SQL Server (Windows auth) |
| Architecture | Clean architecture: `ERP.Domain` -> `ERP.Application` -> `ERP.Infrastructure` -> `ERP.Api`; `ERP.Modularity` holds `IModule` + registry |
| API | Every endpoint returns the `ApiResponse<T>` envelope `{ success, statusCode, message, data, errors, traceId }` (RFC 7807 compatible errors). Core routes `/api/v1/...`; module routes `/api/v1/m/{moduleKey}/...` |
| Frontend | **Angular 19 standalone** components + **ng-zorro-antd 19.3.1**, theme CSS variables `--erp-*`, EN/AR i18n with RTL |

## 1.2 Solution structure

```
backend/
  ERP.sln, Directory.Build.props, Directory.Packages.props, global.json
  src/ERP.Domain            entities, enums, domain rules (no dependencies)
  src/ERP.Application       MediatR commands/queries + handlers, validators, abstractions
                            (areas: Auth, Catalog, Common, Customization, Dashboard, Finance, Imports,
                             Organizations, Parties, Platform, Portal, Printing, Reminders, Search,
                             Security, Settings, Templates, Workflow)
  src/ERP.Infrastructure    EF Core (AppDbContext), migrations, repositories, services
  src/ERP.Api               minimal-API endpoints (Endpoints/*.cs), Program.cs, appsettings.*
  src/ERP.Modularity        IModule contract and module registry
  modules/ERP.Modules.RealEstate | ERP.Modules.Inventory | ERP.Modules.Hr
  countrypacks/ERP.CountryPacks.AE | GB | IN | PK | SA | US
  tests/ERP.UnitTests | ERP.IntegrationTests
frontend/
  src/app/core      api, auth, custom-fields, guards, http, i18n (dictionaries.ts, phrases.ar.ts,
                    translate-now.ts, translate.pipe.ts), services, session
  src/app/layout    shell.component.ts, global-search.component.ts, notification-drawer.component.ts
  src/app/pages     dashboard (Overview), parties, print, reports, portal, admin/* (finance, CoA, settings...)
  src/app/modules   realestate/*, inventory/*, hr/*
  src/app/shared    paged-table, pagination-bar, expand-search, reveal.directive (erpReveal),
                    record-drawer, detail-page, table-tools, simple-chart, party-statement, reason-prompt
database/scripts    SQL scripts matching migrations
deploy/             api.Dockerfile, docker-compose.yml, windows/*.ps1 (deploy, env ops), cloudflare/ (named tunnel)
tools/              qa-smoke.mjs, seed-demo.mjs, smoke-i18n.mjs, smoke-rtl.mjs
docs/               ARCHITECTURE, CONTRIBUTING, GAP-ANALYSIS, REAL-ESTATE, ROADMAP, devops/, team/, handoff/
```

## 1.3 Run locally (Muhammad's Windows PC)

1. **Database:** local SQL Server, database `ERP_Dev`, Windows authentication (`appsettings.Development.json`). In Development the API creates `ERP_Dev` if missing, applies EF migrations and runs the dev seed at startup.
2. **API:** Visual Studio 2022, launch profile **`ERP.Api`** -> `http://localhost:5080` (or `dotnet run --launch-profile ERP.Api` in `backend/src/ERP.Api`).
3. **Web:** `cd frontend; npx ng serve --proxy-config proxy.conf.json` -> `http://localhost:4200`. `proxy.conf.json` forwards `/api` and `/hubs` to 5080.
4. **Demo data:** `node tools/seed-demo.mjs` once the API is up (safe to repeat). Dev sign-ins are listed in the root `README.md` (dev-only demo credentials; do not reuse anywhere else).
5. Linux/macOS alternative: `./run-dev.sh` with your own connection string via user-secrets or `ConnectionStrings__Default`.
6. **Tunnels:** today a Cloudflare **quick tunnel** (`*.trycloudflare.com`, URL changes on restart) exposes the dev app on 4200. A **named tunnel** setup (permanent hostnames `erp-dev.<domain>` -> 4200, `erp-staging.<domain>` -> 5180, `erp.<domain>` -> 5280) is merged in `deploy/cloudflare/` (PR #58) but not yet run; see section 5.6 and `docs/devops/cloudflare-named-tunnels.md#run-order`.

# 2. Hard rules (non-negotiable)

1. **The core stays generic.** Industry concepts live in modules (`backend/modules/*`, `frontend/src/app/modules/*`); country rules live in country packs (`backend/countrypacks/*`). The vocabulary denylist test `backend/tests/ERP.UnitTests/core-vocabulary-denylist.txt` enforces this for core code.
2. **SQL Server only.**
3. **Never write the reference/demo system's brand name anywhere in the project** (code, comments, docs, commits, PR text, file names). A reference product exists; refer to it only generically ("the reference system").
4. **No secrets** in the repo, PRs or docs (no passwords, tokens, connection strings with credentials, tunnel credentials).
5. **`main` is protected:** every change goes through a PR, merged with **squash merge**. **Never force-push**, never push directly to `main`, never delete protected branches.
6. **`git pull` (or `git fetch` + branch from `origin/main`) before starting work.**
7. **No database data or schema changes** (migrations, SQL scripts, data fixes against `ERP_Dev` or any shared DB) **without the owner's explicit approval.** Back up first.
8. **Remove no existing functionality.** Changes are additive; existing callers keep working.
9. **Do not commit local edits to `frontend/angular.json` or `frontend/package-lock.json`.**
10. **UI standards:**
    - Search fields: icon-only `erp-expand-search` (`shared/expand-search.component.ts`) that expands on hover or click.
    - Table action/toolbar buttons: icon only, label revealed on hover via the `erpReveal` directive (`shared/reveal.directive.ts`).
    - Tables: the shared `erp-pagination-bar` (`shared/pagination-bar.component.ts`) with an always-visible horizontal scrollbar; shared `paged-table`.
    - Loading: ng-zorro loaders/skeletons (no blank panels or charts while loading).
    - i18n: every user-visible string has EN and AR entries in `frontend/src/app/core/i18n/dictionaries.ts` (phrases in `phrases.ar.ts`); layouts must work in RTL.
11. **Only manually entered journals can be reversed.** System-posted journals (from invoices, bills, receipts, payments, module postings) must be undone at their source document (PR #44; reverse returns 409 for source journals).
12. From `AGENTS.md`: endpoint -> one MediatR request -> repository -> DB; nothing outside Infrastructure touches `DbContext`; generic paging/search/sort (`PagedQuery`, `PagedResult<T>`, `ApplyPaging/ApplySearch/ApplySort/ApplyFilters`); every business entity is org-scoped; permissions `<module>.<resource>.<action>` lowercase with `.read`; soft delete for master data; ETag/If-Match concurrency.

# 3. Module status

Status legend: **Complete** = built, merged, builds/tests green; **Partial** = usable but with known gaps; **Pending** = not built.

| Area | Status | Key files | Notes |
|---|---|---|---|
| Platform core (tenancy/workspaces, auth, refresh rotation, lockout, reset, onboarding with industry templates) | Complete | `ERP.Application/Auth`, `Organizations`, `Templates/Templates.cs`, `OnboardingService` | Multi-workspace sign-in; UI calls an organization a "Workspace" |
| Users, roles, permissions | Complete | `ERP.Application/Security/Permissions.cs`, `pages/admin/users.page.ts`, `roles.page.ts` | New finance keys added in 7df8788 (`finance.journals.reverse`, `finance.invoices.void`, `finance.invoices.credit`, `finance.bills.void`) |
| Module system (per-workspace enable/disable) | Complete | `ERP.Modularity`, `pages/modules.page.ts` | Disabled module returns 403 `module-disabled` (Arabic guard text in #55) |
| Parties (customers/vendors/owners) + statement tabs | Complete | `ERP.Application/Parties`, `pages/parties.page.ts`, `party-detail.page.ts`, `shared/party-statement.component.ts` | Customer/vendor ledger statements in #48; payment terms drive due dates (#50, #53) |
| Items, tax codes, currencies, legal entities, business units | Complete | `pages/admin/items|tax-codes|legal-entities|business-units.page.ts` | TRN and trade licence on legal entities (#48) |
| Chart of accounts | Partial | `pages/admin/accounts.page.ts`, `pages/admin/coa/*` (account-panels, account-tree.store, account-detail-drawer, accounts-list), `ERP.Application/Finance/FinanceFeatures.cs`, `AccountTree.cs` | Dynamic panels (#51), derived sub-categories (#54), leaf drawer (#57). **Hierarchical numbering in progress** (5.1). Data is flat (see 6) |
| General ledger: journals, posting, reverse | Complete | `ERP.Application/Finance/JournalFeatures.cs`, `Posting.cs`, `pages/admin/journals.page.ts` | Manual-only reversal (#44), reversal dated today |
| AR: invoices, credit notes, receipts | Complete | `InvoiceFeatures.cs`, `ReceiptFeatures.cs`, `SettlementService.cs`, `pages/admin/invoices.page.ts`, `invoice-detail.page.ts`, `invoice-sheet.component.ts` | Void with reason (3-500 chars, audited), credit note from invoice `POST /api/v1/finance/invoices/{id}/credit-note` |
| AP: bills, payments | Complete | `BillFeatures.cs`, `PaymentFeatures.cs`, `pages/admin/bills.page.ts`, `payments.page.ts` | Bill due date from vendor terms (#53) |
| Cheques / PDC, bank | Complete | `ChequeFeatures.cs`, `pages/admin/cheques.page.ts`, `bank.page.ts` | Cheque details + PDC badge on prints (66385aa) |
| Printing | Complete | `ERP.Application/Printing/PrintFeatures.cs`, `pages/print.page.ts` | Party-name fallback, Unapplied/On account rows, credit-note original invoice |
| Budgets, approvals, audit, attachments, documents, messages, imports | Complete (unverified depth) | `pages/admin/budgets|approvals|approval-rules|audit|documents|messages|import.page.ts` | Email sending: SMTP optional, off by default; document emailing is real since #71 (never tried against a live SMTP server) |
| Custom fields | Complete | `ERP.Application/Customization`, `pages/custom-fields.page.ts` | JSON column per extensible table |
| Reports | Partial | `pages/reports/report-registry.ts`, `report.page.ts`, `pages/admin/ledger-reports.page.ts` | Trial balance etc.; ageing by dimension is BA P1 (5.4) |
| Overview dashboard + stat cards | Partial | `pages/dashboard.page.ts` (Executive Highlights: `.hero-kpis-grid` / `.kpi-card`) | **Unification in progress** (5.2) |
| Shared tables / UX standards | Complete | `shared/paged-table.component.ts`, `pagination-bar.component.ts`, `expand-search.component.ts`, `reveal.directive.ts`, `table-tools.component.ts` | Header global search is not yet expand-search (backlog) |
| i18n EN/AR + RTL | Partial | `core/i18n/dictionaries.ts`, `phrases.ar.ts`, `translate-now.ts`, `tools/smoke-i18n.mjs`, `smoke-rtl.mjs` | Passes 2 and 3 merged (#49, #55); manual Arabic click-through not done |
| Real Estate module | Partial | `backend/modules/ERP.Modules.RealEstate/*`, `frontend/src/app/modules/realestate/*` | Properties, units, unit types, leases (activation, schedule, renewal, termination), billing, deposits, work orders, preventive maintenance, inspections, renewals, listings, leads, deals, acquisitions, owner statements, tenant portal. Lease end-date defaults (#56). BA P1 gaps + pm-schema pending (5.3, 5.4) |
| Inventory module | Partial (unverified depth) | `backend/modules/ERP.Modules.Inventory/*`, `frontend/src/app/modules/inventory/*` | Warehouses, stock, movements |
| HR module | Partial (unverified depth) | `backend/modules/ERP.Modules.Hr/*`, `frontend/src/app/modules/hr/*` | Employees, attendance, payroll, review cycles/reviews |
| Country packs AE, GB, IN, PK, SA, US | Partial | `backend/countrypacks/ERP.CountryPacks.*/*CountryPack.cs` | Supply taxes, custom fields, end-of-service rule only; no accounts or numbering rules. VAT returns / e-invoicing not built |
| CRM | Partial (unverified depth) | migration `Crm` | |
| CI/CD (dev/staging/production) | Partial, **Paused by owner** | `.github/workflows/ci.yml`, `cicd.yml`, `deploy/windows/*`, `docs/devops/*` | Pipeline merged; runner/environments not set up (5.7) |
| Hosting / named tunnel | Partial, **Paused by owner** | `deploy/cloudflare/*`, `backend/src/ERP.Api/PublicOrigin.cs` | Code merged (#58); not run (5.6) |

## 3.1 EF Core migrations (in `backend/src/ERP.Infrastructure/Persistence/Migrations`)

26 migrations, oldest first. `ERP_Dev` reportedly has all 26 applied (latest `FinanceColumnHardening`) per the DB Expert report.

| # | Migration | # | Migration |
|---|---|---|---|
| 1 | 20261022132212_InitialCore | 14 | 20261023045610_Inspections |
| 2 | 20261022145011_RealEstateModule | 15 | 20261023050015_OwnerStatements |
| 3 | 20261022191803_LedgerJournalAmount | 16 | 20261023050718_DocumentVault |
| 4 | 20261022192452_Receivables | 17 | 20261023051929_InventoryStock |
| 5 | 20261022193656_ChequesAndBank | 18 | 20261023052928_Crm |
| 6 | 20261022194501_Payables | 19 | 20261023053134_HrExtras |
| 7 | 20261022200718_RealEstateFinance | 20 | 20261023054857_EmailAttempts |
| 8 | 20261022201459_Acquisitions | 21 | 20261023105111_FinanceAllocationIntegrity |
| 9 | 20261022202642_HrModule | 22 | 20261023105217_FinanceCreditNotes |
| 10 | 20261023035937_Reminders | 23 | 20261023105335_FinanceVoidAudit |
| 11 | 20261023040602_TenantPortal | 24 | 20261023105511_BillBusinessUnit |
| 12 | 20261023041637_RealEstateOps | 25 | 20261023105543_FinanceReferentialFks |
| 13 | 20261023042754_Budgets | 26 | 20261023105812_FinanceColumnHardening |

Migrations 21-26 are the "schema M1-M6" set (commit 7a97a05).

# 4. Completed work log

## 4.1 Pull requests #46 to #78 (all squash-merged into main)

| PR | Commit | Summary |
|---|---|---|
| [#46](https://github.com/juni111shah/ERP/pull/46) | c6e54b7 | CI/CD: named CI jobs, contributor and promotion flow docs, named-tunnel plan, optional PublicUrl in deploy scripts |
| [#47](https://github.com/juni111shah/ERP/pull/47) | 3c7900b | UI: shared advanced pagination bar, sticky horizontal table scrollbar, hover-reveal toolbar buttons and column menus |
| [#48](https://github.com/juni111shah/ERP/pull/48) | f8d6871 | SQA bugs round 2: party statement tabs with customer/vendor ledger endpoints, TRN and trade licence on legal entities, drawer clears ?edit, parties first-load race, route redirects, missing icons, owner demo seed |
| [#49](https://github.com/juni111shah/ERP/pull/49) | 4fdce82 | i18n Arabic pass 2: 675 EN-AR phrases and 114 ui.* keys |
| [#50](https://github.com/juni111shah/ERP/pull/50) | 6e42f65 | Invoice due date follows customer terms on drafts; restricted party ID/IBAN read-only; Arabic for statement/TRN strings |
| [#51](https://github.com/juni111shah/ERP/pull/51) | c47cf41 | Dynamic Chart of Accounts panels with lazy per-level loading and hover-reveal actions |
| [#52](https://github.com/juni111shah/ERP/pull/52) | cd14d41 | Table actions: Refresh, filters, hover-reveal Add buttons and icon-only expanding search across tables |
| [#53](https://github.com/juni111shah/ERP/pull/53) | 442941a | Bill due date follows vendor payment terms on drafts |
| [#54](https://github.com/juni111shah/ERP/pull/54) | 0e43355 | CoA sub-category level (Current/Non-current etc.) so flat charts drill down |
| [#55](https://github.com/juni111shah/ERP/pull/55) | 7c0fa55 | Arabic strings pass 3 (guards, validation, toasts, reports, CoA toolbar) |
| [#56](https://github.com/juni111shah/ERP/pull/56) | 4e23e68 | Leases: default end date (start + 1y - 1d), renewal end prefill, draft end-date follow (backend `Lease.DefaultEndDate`; no schema change) |
| [#57](https://github.com/juni111shah/ERP/pull/57) | 9c93a63 | CoA: leaf accounts open a details drawer (Edit, Add sub-account, Deactivate; RTL) |
| [#58](https://github.com/juni111shah/ERP/pull/58) | 87d9c2f | Hosting: optional public URL (`Hosting:PublicUrl`) + Cloudflare named-tunnel setup script |
| [#60](https://github.com/juni111shah/ERP/pull/60) | db19f90 | Hierarchical CoA numbering (Option A) + SQA H-1, M-A, M-B (partial) |
| [#61](https://github.com/juni111shah/ERP/pull/61) | 8adb557 | Record history tab, AR ageing by business unit/source, expand-search header search |
| [#62](https://github.com/juni111shah/ERP/pull/62) | 2f20813 | Unified `erp-stat-card`, skeleton loading, locale-aware numbers |
| [#71](https://github.com/juni111shah/ERP/pull/71) | 78c57e9 | Real document emailing (invoice, receipt, bill, payment, party statement) via `IEmailSender`, `core.email.send`, logged in the message log; replaces the modal that only called `/messages/test-email` |
| [#72](https://github.com/juni111shah/ERP/pull/72) | 36c572a | Inventory: stock counts, valuation/movement/slow-moving/reorder reports, warehouse edit, audit fixes, search, low-stock reminder. Migration `InventoryCounts` + `0021_InventoryCounts.sql` (not applied to any DB) |
| [#73](https://github.com/juni111shah/ERP/pull/73) | 8c3ec67 | Country-pack tax returns (AE VAT201, SA, GB VAT100, IN GSTR-3B, PK, US) + non-blocking invoice tax-number warnings; no schema change |
| [#74](https://github.com/juni111shah/ERP/pull/74) | a8de899 | HR leave management (types, balances, requests, holidays, attendance/payroll feed). Migration `HrLeave` + `0022_HrLeave.sql` (not applied to any DB) |
| [#76](https://github.com/juni111shah/ERP/pull/76) | 2e265e8 | Owner cost basis: work order ChargeableTo / OwnerSharePercent / BillId; owner statement uses bill-linked cost and the owner's share. Migration `OwnerCostBasis` + `0023_OwnerCostBasis.sql` (not applied to any DB) |
| [#77](https://github.com/juni111shah/ERP/pull/77) | ba0c341 | Cheque bounce follow-up (RE-PM-08) and replacement re-link (RE-PM-09): BouncedOn, FollowUpOn, FollowUpNote; reminder 3 days after a bounce with no follow-up. Migration `ChequeBounceFollowUp` + `0024_ChequeBounceFollowUp.sql` (not applied to any DB) |
| [#78](https://github.com/juni111shah/ERP/pull/78) | (see git log) | Party groups (`PartyGroups`, `Parties.PartyGroupId`, ageing by party group) and reuse of soft-deleted account codes (filtered unique index). Migration `PartyGroupsAndCodeReuse` + `0025_PartyGroupsAndCodeReuse.sql` (not applied to any DB) |
| [#86](https://github.com/juni111shah/ERP/pull/86) | ac661c8 | Overview date range: `GET /dashboard?from=&to=` (optional, default last year ending today, max 10 years, 400 on from > to). Billed vs collected, collected, net result and expenses follow the period; cash is at the end of the period; receivables, ageing, payables, cheques, approvals and module cards stay current. UI: preset select (1M/3M/6M/1Y/2Y/3Y/custom, default 1Y), range picker (no future dates, max 5 years), narrow/widen/refresh buttons; `?range=` or `?from=&to=` in the URL plus `localStorage` `erp.overview.range`. EN/AR `ui.overview.range.*`. No schema change |
| fix/overview-range-review | (see git log) | Overview range review fixes (ui ux Expert + Advanced UI reviews of #86) and owner redesign. **Control:** one dropdown (Last month, Last 3 months, Last 6 months, Last year = trailing 12 months, Last 2 years, Last 3 years, Custom) + Refresh; Narrow/Widen buttons and their keys removed; the range picker shows only for Custom (opens automatically, read-only inputs, no future dates, dates more than 5 years from the first pick disabled, inline hint on rejection) and hides again on a preset. Range row sits on its own line above the toolbar, visible while loading or after an error, with a localized `{from} – {to}` caption (`<bdi>`) and an `aria-live` announcement. Static "All Legal Entities • as of" toolbar text removed (it was not a control). Load error shows a Retry panel instead of an endless skeleton; empty period shows a notice. **API:** presets start one day after the same day n months back (1Y = 365 days), max **5 years** (was 10), future `to` rejected (1 day slack), validator uses `IClock`; billed-vs-collected chart uses whole weeks (≤3 months) / months (≤2 years) / quarters, counted back from `to` (1Y = 12 bars), and sends `buckets` + `granularity` so the client labels the axis in EN/AR; cash KPI sends `captionKey` `kpi.cash.caption` + `captionArgs.date` (translated client-side). URL always carries the period (`replaceUrl` on first load); URL and `localStorage` ranges are validated (real dates, ≤ 5 years, not future). Week start unchanged (Sunday, Qatar). No schema change |
| mobile/design-refresh | (see git log) | Mobile (Flutter) visual restyle to the owner-approved mockup. Central theme `mobile/lib/theme/` (`tokens.dart` colours/radii/spacing + `ErpTheme` ThemeExtension, `app_theme.dart` Material 3 light/dark), bundled Inter + Noto Sans Arabic fonts (OFL, `mobile/assets/fonts`, no google_fonts runtime). New kit widgets in `mobile/lib/kit/`: notched animated bottom nav (`bottom_nav.dart`, RTL mirrored, needs `Scaffold(extendBody: true)`), stat card/grid, billed-vs-collected bar chart, list card, status tag, detail hero, module grid, filter chips, expandable search, summary strip. Restyled: home shell (gradient logo, workspace subtitle, floating bar), Overview (stat grid, chart panels, "Needs attention" alerts), invoice/receipt/customer lists (cards with amount, status tag, overdue + paid progress), invoice detail hero, More (module grid, web-only areas marked "web"), login and workspace picker. List padding and FAB clear the floating bar. Same 5 tabs, all screens kept, **API calls unchanged**, no schema change. Not used yet: action bar (tests rely on button types), summary strip (no summary endpoint). Specialist widgets are light-mode only, so dark mode is partial. New test `mobile/test/bottom_nav_test.dart` (LTR/RTL notch, slide animation, tap index) |
| mobile/overview-4-card-scroll | (see git log) | Mobile (Flutter) Overview only: KPI carousel (2x2 pages, snapping, dots + prev/next arrows, RTL-mirrored) under category chips (All + first link segment, counts; chip change resets to page 1), then charts and alerts. Compact values with full value on long-press, stable per-KPI stripe, tappable KPI cards/alerts when the link maps to an in-app screen, chart labels abbreviated/rotated (full label on tap), skeleton/empty/error states. New `kit/stat_carousel.dart`, `features/dashboard/overview_logic.dart`; `Kpi`/`DashboardAlert` parse the existing `link`. Follow-up: header gear replaced by bell (unread count) + initials avatar (unread dot) opening a draggable account sheet (user/workspace, Notifications, Appearance Light/Dark/System via `themeModeProvider` persisted under `theme_mode` in the existing secure key-value store, Switch workspace, Settings, About, Sign out with confirm); notifications sheet reads `GET /api/v1/notifications` (falls back to Overview alerts, nothing marked read); filter chips + bottom nav use theme tokens for dark mode; chart legend wraps instead of truncating. No API/web changes. PR #90 merged into main (5595eb3). |
| mobile/overview-polish-v3 | (see git log) | Mobile (Flutter) polish only, from main 5595eb3: one type scale (`ErpType`) and spacing scale (`ErpSpace` incl. half steps) applied across all mobile screens; shared `kit/state_view.dart` (`ErpStateView` error/empty with Retry / Clear filter / Clear search) for network errors, empty and filtered lists, notifications and charts; `kit/skeleton.dart` (`ErpSkeleton` shimmer, no packages) for Overview, lists, detail hero and notifications instead of page spinners (inline button spinners and splash kept); `kit/motion.dart` (`ErpMotion` 150-300ms, `ErpPressable`, `ErpFadeSlideIn`, `ErpPulse`) and `kit/segmented.dart` (animated thumb). Carousel eased snap, animated dots, arrows fade at ends, press-scale cards, haptics on page/chip change, cards fade in on chip change; profile sheet eased open, staggered rows, press feedback, segmented Appearance, bell badge pulse. All motion off under `MediaQuery.disableAnimations`. New `test/polish_test.dart`. No API/web/DB changes. PR open, not merged. |
| feature/first-login-2fa | (see git log) | First-sign-in two-factor (TOTP, RFC 6238: SHA1, 6 digits, 30 s) on web and mobile. `POST /auth/login` without a confirmed authenticator returns no session, only `twoFactorSetupRequired` + a 10-minute scoped `challengeToken` (Data Protection, bound to the user's password hash); `POST /auth/login/2fa/setup` returns the `otpauth://` link (issuer from `Auth:TwoFactor:Issuer`), the grouped key and a server-side QR PNG (QRCoder); `POST /auth/login/2fa/verify` checks the code (±1 step, last-used step refused), turns 2FA on, returns 8 recovery codes and the normal session. Returning users get `twoFactorRequired` + a challenge and use the same verify call. Wrong codes count towards Identity lockout (5, then 423). Flag `Auth:TwoFactor:Enforce` (code default **true**; `appsettings.Development.json` false; turn on locally with `Auth__TwoFactor__Enforce=true` or set it in appsettings). Web: `erp-two-factor-step` (QR, "Can't scan? Show key" + copy, 6-digit auto-submit, recovery codes, EN/AR, RTL). Mobile: `TwoFactorStep` (QR from data URI, Show key + copy, `oneTimeCode` autofill, 48dp, dark/RTL). **No schema change** (key and last step in `AspNetUserTokens`). Staff portal only; tenant portal sign-in unchanged |
| mobile/palette-onboarding-touch | (see git log) | Mobile (Flutter) only, from main: command palette (`kit/command_palette.dart`, `core/palette_registry.dart`; header Search button, long-press Overview tab, Ctrl/Cmd+K); onboarding coach tour (`features/onboarding/onboarding_tour.dart`, flag `onboarding_tour_v1_seen`, shown once after sign-in, "Show the tour again" in the profile sheet; separate from the intro tour `intro_tour_v1_seen`); plain labels for 130 UI captions from `JARGON_USAGES.md` section 1 (`core/plain_labels.dart`), server values/keys/roles/permissions/screen ids untouched; touch targets (`kit/tap_target.dart`, audit fixes C1 C2 H1 H2 H3 M1 M3, M2 not applied) with `test/tap_targets_test.dart` covering every screen LTR+RTL plus the palette and tour. No API/web/DB changes. PR open, not merged. |

Earlier relevant commits on main: 7fe89dd (#44, manual-only journal reversal), 208acd3 (#45, SQA UI fixes), 7df8788 (void reasons in audit log, credit notes linked to invoices, new permissions, reverse guard, print fallbacks, unapplied allocations), 7a97a05 (schema M1-M6), 29362d9 (dev/staging/production pipeline).

## 4.2 Non-PR items (done)

| Item | Result |
|---|---|
| IsTaxRegistered data fix | 2 legal-entity rows in `ERP_Dev` set to tax registered (they had a TRN but the flag was off; SQA U-3). Script: `/workspace/erp-team/db/finance-check/fix-data.sql` (box path) |
| Demo seed run | `tools/seed-demo.mjs` run against `ERP_Dev` (demo-re workspace sample data) |
| Product Mind report | `/workspace/erp-team/product/world-class-report.md`: gap analysis vs market leaders, weighted overall **maturity about 2.3 / 5**, phased roadmap and top 10 recommendations; UI recommendations ranked and sent to the owner |
| CoA numbering investigation | `/workspace/erp-team/merge/coa-numbering-findings.md`: codes are free text (max 20), unique per workspace, not tied to `ParentId`; Option A recommended |
| Cheque print | Already in main as 66385aa (cheque instrument details + PDC badge on receipt and payment prints); verified at 9c93a63, no patch needed |

## 4.3 Server-side PDF for print documents (branch `feature/server-pdf`, open PR, 2026-11-03)

- **Backend:** `GET /api/v1/print/{invoice|receipt|bill|payment|journal}/{id}/pdf` returns `application/pdf` with a `Content-Disposition` file name (for example `Invoice-INV-0007.pdf`). Same permissions and 404 rules as the JSON print routes (it reuses the `GetPrint*` queries via `GetPrintPdf`).
- **Design:** `IPrintPdfRenderer` in `ERP.Application/Printing/PrintPdf.cs`; `QuestPdfPrintRenderer` in `ERP.Infrastructure/Printing` (QuestPDF 2026.9.1, **Community licence**: free for organisations under USD 1M annual gross revenue; a larger company needs a QuestPDF Professional/Enterprise licence). Arabic/RTL text uses embedded Noto Sans Arabic (SIL OFL 1.1, licence file next to the fonts). No schema or data changes.
- **Web:** icon-only "Download PDF" button (tooltip) on the print page for those five documents; browser print is unchanged.
- **Mobile:** "Print / Share PDF" app-bar button on invoice and receipt details: downloads the server PDF (`ApiClient.getBytes`) and hands it to the OS print dialog or share sheet through the `printing` package (behind the injectable `PdfOutput`). No PDF is built on the phone (`pdf` is only a transitive dependency of `printing`).
- **Tests:** `PrintPdfRendererTests` (unit), `PrintPdfTests` (integration: content type, file name, 404, 401, same policies), `mobile/test/pdf_share_test.dart`.

## 4.4 Mobile first-open intro tour (3D depth, light) - PR #93 OPEN, not merged (branch `mobile/intro-tour-3d`)

- Five-screen tour shown once on first app open, before sign-in: overview, cash flow (billed vs collected), invoicing, alerts and settings, everything in one app. Light ErpTheme forced; EN + AR; 48dp targets; system back = Skip (screen 1 only since 4.6). Code in `mobile/lib/features/intro/`.
- Flag `intro_tour_v1_seen` = `1` in the KeyValueStore (`storeProvider`), set by Skip / Get started / back. `introProvider` (unknown -> splash, show -> `/intro`, seen -> normal session routing) is wired into the go_router redirect in `lib/app.dart`. Kept separate from the later coach-mark tour (`onboarding_tour_v1_seen`).
- Motion values come from the Advanced UI UX spec (`intro_motion.dart`): one ticker, per-layer Matrix4 transforms (perspective 1/1100), staggered rise, perspective floor + brand-tinted glow, page parallax, 6s drift eased out after 12s; captions fade and slide with the parallax; reduced motion = flat + 200ms crossfade. No new packages, no goldens.
- v2: existing ERP mark + name in the screen 1 header (small mark after), page dots + Next / Get started bar, outcome headline + one line per screen, screen 5 "Everything in one app" chips listing only features the app has. Card text scales down to one line instead of cutting off. Arabic copy needs a native-speaker review.
- Tests: `test/intro_tour_test.dart` (7), `test/intro_motion_test.dart` (4), `test/intro_fit_test.dart` (3: no truncated text or overflow at 360x780 and 390x844, EN + AR, real fonts).

## 4.5 Mobile HigherSquare 360 branding and app icon - MERGED (PR #96, branch `mobile/brand-hs360`)

- Brand reused from the web app (`frontend/public/highersquare-*.png`): name **HigherSquare 360** (one word, as on the web), emblem = green rounded tile, white disc, open 360 ring, four bars and a breakout trend arrow. Web wordmark colours kept: "HigherSquare" #359867 / "360" #215A67 (dark: #4ADE80 / #38BDF8).
- Single source: `mobile/lib/core/brand.dart` (`Brand.name`, `Brand.launcherLabel` = "HS 360", `BrandColors`). Vector mark: `mobile/lib/kit/brand_mark.dart` (`BrandMark` CustomPainter, `BrandWordmark`, `BrandLockup`, `BrandSplash`). SVG masters: `mobile/assets/brand/hs360-mark.svg`, `hs360-icon-foreground.svg` (not bundled; sources for icons).
- Used on: splash, intro screen 1 (mark + name; small mark on screens 2-5), sign-in (mark + name above the form), workspace picker (lockup), home header (mark replaces the workspace initials tile), profile About dialog, MaterialApp title.
- Launcher: Android mipmap mdpi..xxxhdpi `ic_launcher` + `ic_launcher_round`, adaptive `mipmap-anydpi-v26` (foreground PNG in the 66dp safe zone, background `@color/ic_launcher_background` #4FA077), launch_background shows the mark; iOS AppIcon (all slots, opaque) + LaunchImage. Home-screen label "HS 360" (Android `android:label`, iOS `CFBundleDisplayName`) because the full name truncates under launcher icons. Generated on the box from the SVG master with cairosvg; no new packages. Web favicon untouched.
- Tests: `test/brand_test.dart` (constants, manifest/plist labels, icon files, mark sizes + semantics, wordmark colours light/dark, splash, sign-in, 320dp fit, workspace picker, home header); `test/intro_tour_test.dart` now expects `Brand.name`.

## 4.6 Mobile intro tour back navigation - PR #97 OPEN, not merged (branch `mobile/intro-back-nav`, 2026-11-03)

- Screens 2-5 of the intro tour now have a round 48dp **Back** control at the start of the dots / Next bar (semantics "Back", hint "Previous screen"; Arabic "رجوع" / "الشاشة السابقة"; arrow mirrors in RTL). Not shown on screen 1.
- Swipe to the previous screen works (right swipe in LTR, left in RTL); swiping past screen 5 does not finish the tour.
- Android system / predictive back on screens 2-5 goes to the previous screen and does **not** set `intro_tour_v1_seen`; on screen 1 it still counts as Skip. Skip and Get started unchanged. Returning to a screen replays its entrance, then the ticker stops again.
- Navigation code only (`intro_page.dart`, two strings in `intro_strings.dart`); header and HigherSquare 360 brand mark + name (PR #96) untouched. Tests: `test/intro_tour_test.dart` now 14 (8 new/changed).

# 5. Pending tasks

Owner and coordinator: unless stated, the specialist agent prepares a patch or branch and Grok Bot builds, opens the PR and squash-merges.

## 5.1 Hierarchical CoA numbering (Option A) - DONE (PR #60)

- Setting `finance.coa.numbering` (`free` | `hierarchical`, segments, separator) in the existing Settings table; no schema change. Endpoints `GET/PUT /api/v1/finance/coa-numbering` and `GET /api/v1/finance/accounts/next-code?parentId=&type=`. Code in `AccountCodeMask.cs` (Domain) and `AccountNumbering.cs` (Application); UI: auto-filled editable Code on New account / Add sub-account, mask editor in Settings; EN + AR.
- Validation on create/update (hierarchical mode only): mask, parent prefix at the right level, child type equals parent type, first child under an account with postings is blocked (409), system account codes immutable, unique-index duplicate becomes a 409 with one automatic retry in the form. Free mode unchanged.
- Tests: `AccountNumberingTests` (unit) and `AccountNumberingIntegrationTests`. **Not done:** UI was not clicked through in a browser; Option B stays in the backlog (5.10); soft-deleted code reuse needs a filtered unique index (schema, needs owner approval).

## 5.2 Stat card unification + page-load fixes - DONE (PR #62)

- Shared `erp-stat-card` (`shared/stat-card.component.ts`, `theme/_stat-card.scss`, `.erp-stat-grid`) with ng-zorro skeleton loading, EN/AR + RTL, bdi-isolated numbers. Used by the Overview (hero, compact and full grids; stale dashboard responses are cancelled), invoices, cheques, budgets, report summaries, shared detail pages (invoice, bill, receipt, payment, journal, party), acquisitions, lease/property detail, renewals and inventory stock. `LOCALE_ID` follows the saved language; `I18nService.date()` added.
- Verified with `tools/smoke-stat-cards.mjs` (12/12) against a **mocked API** (the real API needs SQL Server, which the sandbox lacks); please check against `ERP_Dev` on the PC. Pages with no stat cards of their own (billing results, payroll detail, HR pages) were not converted.

## 5.3 DB schema migrations (pm-schema) - PENDING (needs `pm-schema-proposal.md`; not available to the AI agent that did #60-#74)

- Migrations 27-31 (`InventoryCounts`, `HrLeave`, `OwnerCostBasis`, `ChequeBounceFollowUp`, `PartyGroupsAndCodeReuse`; scripts 0021-0025) exist in the repo and are **not applied to `ERP_Dev`**; apply only after a backup and owner approval.
- Not started (no agent could read the proposal; PartyGroups itself now exists from #78): CoreFx, CorePenaltiesDunning, CoreReasonsAuditInstruments, CoreCustomerDeposits, CoreInstalmentPlans, RealEstatePm, deposit backfill. The BA P1 stories are now all done (RE-PM-42 and RE-PM-30 in #61, RE-PM-08/09 in #77, RE-PM-20/21/22 in #76) and so are party groups and soft-deleted account code reuse (#78).

- **Owner:** DB Expert (prepares EF migrations); Grok Bot builds, merges, then applies to `ERP_Dev` **only after Muhammad approves** and after a backup.
- **Sources:** `/workspace/erp-team/db/pm-schema-proposal.md` (status: PROPOSAL, nothing applied), SQL drafts `/workspace/erp-team/db/pm-sql/01_core_fx.sql` ... `99_smoke_test.sql` (guarded to run only on `*_Scratch` databases).
- **Planned order (each step one additive EF migration):** CoreFx; CorePenaltiesDunning (incl. PartyGroups); CoreReasonsAuditInstruments; CoreCustomerDeposits; CoreInstalmentPlans; RealEstatePm (incl. rename `re_Leases.GraceDays` -> `RentFreeDays` with API alias); RealEstateDepositBackfill (data, idempotent); AePack; demo owner parties (seed, not migration); later drop legacy deposits.
- **Next steps:** owner reviews the 10 BA recommendations in the proposal; DB Expert converts approved steps into EF migrations in `ERP.Infrastructure/Persistence/Migrations` + matching `database/scripts`; build/test; PR; apply to backed-up `ERP_Dev`.
- **Acceptance criteria:** migrations are additive, existing rows stay valid, backfill report shows 0/0 mismatches, all tests pass, denylist test passes (core has no industry words), owner sign-off recorded.

## 5.4 Business analyst P1 stories - DONE (#61, #76, #77, #78)

Source: `/workspace/erp-team/ba/pm-workflows-gap.md` (section 14 "Top P1 stories"). Mapping of short names to story IDs below is best-effort **(unverified)**.

| Story | Owner | BA ref | Acceptance (summary) |
|---|---|---|---|
| Record history (**DONE**, PR #61: `GET /api/v1/audit/record`, `erp-record-history`, History tab/card on detail pages; business events not included) | Full Stack Developer Expert | RE-PM-42 | History tab on any record shows who, when, action, old -> new per field, plus business events; gated by read + `audit.read` |
| Ageing by dimension/source (**DONE** for business unit and SourceType, PR #61; party group skipped: no PartyGroup concept until 5.3) | Full Stack Developer Expert | RE-PM-30 | AR ageing filterable by business unit, party group and SourceType; module dimensions via SourceType/SourceId lookup, no property columns in core |
| Cheque bounce follow-up + reopen | Advanced UI ux expert | RE-PM-08 (and RE-PM-09 replacement cheque) | Bounced cheque reopens the receivable, triggers generic follow-up; replacement cheque can be re-linked |
| Owner cost basis | Advanced UI ux expert | RE-PM-20/21/22 | Owner statement uses bill-linked actual cost and cost responsibility (ChargeableTo) correctly |

Next steps: each owner delivers a patch (`git diff --binary origin/main..HEAD`) in `/workspace/erp-team/<agent>/`, built and tested; Grok Bot merges. Stories needing new columns depend on 5.3 and owner approval.

## 5.5 finance-check-2 - verify and close

- **Owner:** Full Stack Developer Expert.
- **Scope:** void reason, credit-note link, statement, journal reverse guard, print fallbacks, unlinked (Unapplied/On account) allocations.
- **State:** the backend change set is in main via commit 7df8788 (verified in code at 87d9c2f: `VoidMemo`, `finance.journals.reverse`, `ReverseTodayAsync`, `AllocationLabels.Unapplied`, `POST /finance/invoices/{id}/credit-note`). Remaining: confirm the open SQA findings in `/workspace/erp-team/sqa/full-check/report.md` (M-1 reverse guard is a denylist, M-2 credit note from fully paid invoice cannot be applied, M-3 blank document number on unapplied rows, L-1 reason prompt min length) are fixed or still open **(current state unverified)**, and run the UI flows that need test documents.
- **Files:** `fullstack/finance-check-2/CHANGES.md`, `merge/finance-check-2-combined.patch`, `fullstack/finance-check-2/grant-new-permissions.sql` (DB change: needs approval).
- **Acceptance:** void without/short/over-long reason returns 400; audit `Voided` row with reason; credit note linked and capped; reversing a source journal returns 409; prints add up with unapplied rows labelled; tests pass.

## 5.6 Permanent links (named tunnel) - PAUSED BY OWNER

- **Owner:** Muhammad (only he can do these); dev ops supports.
- **Code:** merged in #58: `deploy/cloudflare/setup-named-tunnel.ps1` (idempotent, `-WhatIf`), `config.example.yml`, `backend/src/ERP.Api/PublicOrigin.cs` (env `ERP_Hosting__PublicUrl`), notes in `docs/devops/cloudflare-named-tunnels.md`. Scripts are untested against real Cloudflare/Windows.
- **Owner steps:** (1) add his domain to Cloudflare, switch nameservers, wait for zone Active; (2) run `powershell -NoProfile -ExecutionPolicy Bypass -File <checkout>\deploy\cloudflare\setup-named-tunnel.ps1 -Domain <domain>` (try `-WhatIf` first) and approve `cloudflared tunnel login` in the browser; (3) run the same with `-InstallService` in an **elevated** PowerShell, then once more normally; (4) later `-StopDevQuickTunnel` once SQA has the new dev URL.
- **Acceptance:** `erp-dev.<domain>`, `erp-staging.<domain>`, `erp.<domain>` serve the right apps and survive reboot.

## 5.7 CI/CD - PAUSED BY OWNER

- **Owner:** dev ops (prepares), Muhammad (tokens/admin settings).
- **Remaining:** fast-forward the `dev` branch to main; register the self-hosted Windows runner (label `erp-deploy`, needs a runner token from the owner); create GitHub environments (dev/staging/production) and the rulesets proposed in `docs/devops/manual-steps.md`. Pipeline described in `docs/devops/pipeline.md`; workflows `.github/workflows/ci.yml`, `cicd.yml`.
- **Acceptance:** push to dev/staging deploys automatically; production deploys only commits already on staging; promotions are ff-only, never forced.

## 5.8 SQA review - H-1 and M-A fixed, M-B partly (PR #60)

- **Fixed:** H-1 (CoA tree cache now keyed by workspace + user + legal entity + balance access and reset on session change), M-A (roll-up ignores children of a different account type).
- **M-B partly:** the balance path no longer reads every parent link; `GetAccountCategories` still sums journal lines for all accounts.
- **Still open:** Lows L-A..L-H (report not available to the agent), the written SQA review per item, SQA write tests (on hold).

## 5.9 On hold

- SQA write tests (Playwright under `/workspace/erp-team/sqa/playwright`).
- Cloud-agent checklist build.
- JS voice-assistant proposals: skipped by the owner; not part of this project.

## 5.10 Backlog

- Hierarchical numbering **Option B**: real header accounts + renumbering existing codes. Changes existing data (needs owner approval and backup), breaks ~54 hard-coded codes in tests, `seed-demo.mjs`, `qa-smoke.mjs`; +2-3 days.
- ~~Convert the header global search to the expand-search pattern~~ done in PR #61 (`data-testid="global-search"` stays on the real input; new `global-search-toggle`; `tools/qa-smoke.mjs` updated).
- Product Mind roadmap phases 0-3 (`product/world-class-report.md` section 6).

# 6. Known issues and gotchas

- **CoA data is flat:** all 73 accounts in `ERP_Dev` have `ParentId = NULL`, 4-digit codes 1000-7900. The Assets / Current assets levels in the tree are virtual: level 1 = AccountType, level 2 = sub-category derived in code (`StandardAccountSubcategoryRules`, #54). `ParentId` has no FK and no index.
- **Soft-deleted account codes block reuse:** the unique index `(OrgId, Code)` is unfiltered, so a deleted account's code passes the app check but fails at the DB with a unique-index error.
- **`ng serve` goes stale after branch switches:** restart it (and hard-reload the browser) after checkout/merge.
- **`tools/qa-smoke.mjs` types into the header global search** (`data-testid="global-search"`); changing that component breaks the smoke test unless updated.
- **PowerShell 5 on the PC does not support `&&`:** use `;` (and check `$LASTEXITCODE`).
- Journals and budget lines reference accounts by `AccountId`, not code, so code changes do not break postings.
- The box (Linux) has no SQL Server; integration tests that need SQL Server or the running app must be run on the PC (unverified whether the box has a dotnet SDK at any moment; it varies).
- `/workspace/erp` on the box is used by in-progress work; use a separate `git worktree` for new tasks.
- SMTP off by default; document emailing works through `IEmailSender` (#71) but has not been tried against a live SMTP server.

# 7. How to pick up a task

1. **Sync:** `git fetch origin` then `git worktree add ../erp-<topic> -b <tool>/<topic> origin/main` (branch prefixes: `claude/`, `antigravity/`, `cursor/`, `codex/`, `feat/`, `fix/`, `docs/`).
2. **Read:** `AGENTS.md`, this file, and the relevant spec in `docs/team/` or `/workspace/erp-team/`.
3. **Build and test:**
   - `dotnet build backend/ERP.sln` (0 errors)
   - `dotnet test backend/ERP.sln` (all unit and integration tests pass; integration needs SQL Server)
   - `cd frontend; npm ci; npx ng build` (0 errors; existing unused-pipe warnings are known)
   - Optional: `node tools/qa-smoke.mjs`, `node tools/smoke-i18n.mjs`, `node tools/smoke-rtl.mjs` against a running app.
4. **Hand-off as a patch (specialist agents):** commit locally, then `git diff --binary origin/main..HEAD > /workspace/erp-team/<agent>/<topic>.patch`; verify with `git apply --check` on a clean `origin/main`; report files, build/test results and anything not run.
5. **Merge (coordinator):** push the branch, open a PR into `main` (conventional commit title such as `feat(finance): ...`), wait for CI, **squash-merge**, delete the branch, remove the worktree. Never force-push; never commit `angular.json` / `package-lock.json` edits; never include secrets or the reference system's name.
6. **DB changes:** EF migration + `database/scripts` SQL in the PR; apply to `ERP_Dev` only after owner approval and a backup.
7. **Update this file** (`docs/handoff/PROJECT-STATUS.md`) in the same PR or a follow-up docs PR when a task changes state; mirror to `/workspace/erp-team/STATUS.md`.

# 8. Last updated

- **2026-10-23 17:55 PKT (Asia/Karachi, UTC+5)**
- main at time of writing: **87d9c2f** (PR #58). The commit that adds this file is the docs PR after #58.
