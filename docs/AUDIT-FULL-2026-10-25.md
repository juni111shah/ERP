# ERP complete audit report (system level)

Audit date: 2026-10-25. Repository: `juni111shah/ERP`. Branch audited: `antigravity/dashboard-and-modules`, fast-forwarded to `origin/main` at `9574f90` (PR #70, includes the other agent's fixes up to `f68192b`).
Companion documents (page-level detail, still valid unless a finding below says otherwise): `docs/AUDIT-2026-10-25.md` (first pass) and `docs/AUDIT-DEEP-2026-10-25.md` (page-by-page, items A1-A13, B1-B11, C1-C11, D). This report does not repeat every page-level item; it adds the system, security, database, build and verification layers and re-checks status at the new HEAD.
No code, database, migration or configuration was changed. No secrets are quoted.

## 1. Executive summary

| Area | Health | Summary |
|---|---|---|
| Build and tests | Good | Backend builds with 0 errors; 379 backend tests pass (212 unit + 167 integration); `ng build` succeeds; 2 frontend specs pass. |
| Architecture | Good foundation | Clean layering, MediatR pipeline with validation and authorization behaviors, per-org global query filter, RowVersion on every org entity, module gate, rate-limited login, short-lived access tokens with refresh rotation. EF model and migrations are in sync (verified with `dotnet ef migrations has-pending-model-changes`). |
| Older modules (finance, real estate, HR, inventory) | Reasonably complete | Best tested and documented. |
| Nine newer modules (Sales, Purchasing, POS, Retail, CRM, Projects, Services, Distribution, Manufacturing) | Not production-ready | Standalone CRUD with partial finance/stock hooks, no integration tests, no i18n, no UI permission gating, fake or placeholder ids in several forms, state transitions unchecked, inconsistent accounting. Other agent is actively fixing items; several earlier findings are now fixed (see section 11). |
| Security | Acceptable core, notable gaps | No critical issue found in the read code. Gaps: attachment access is not tied to the owning record, portal tokens can reach authenticated-only core endpoints, runtime Angular dependency vulnerabilities, committed dev credentials, single global rate limit policy only on login, in-memory token revocation list. |
| Verification limits | Material | App was never started; no browser, E2E or load testing was possible in this audit. See section 18. |

Counts: 109 page components + 26 shared/layout components, about 459 route registrations across 29 endpoint files (regex count, accurate to about 5%), 170 permission keys, 12 modules, 6 country packs, 27 EF migrations, 20 SQL scripts, 37 `DbSet`s in the core context plus module-owned tables.

## 2. Repository inventory and architecture

- **Backend** (`backend/`): ASP.NET Core 8 minimal APIs; projects `ERP.Domain`, `ERP.Application` (MediatR, FluentValidation), `ERP.Infrastructure` (EF Core, SQL Server, Serilog, SignalR hub), `ERP.Api` (host, endpoints, seeders), `ERP.Modularity` (`IModule`, registry, module gate), 12 module projects, 6 country packs, 2 test projects. Config: `appsettings.{,Development,Staging,Production}.json`; env overrides with prefix `ERP_`.
- **Frontend** (`frontend/`): Angular 19.2 standalone components, ng-zorro-antd 19.3.1, apexcharts 8.4, papaparse. 185 non-spec source files. Routes in `app.routes.ts` (237 lines) plus `realestate.routes.ts` and `hr.routes.ts`. HTTP interceptors: auth, entity-scope, envelope, module-disabled. Token is held in memory (signal); workspace id in `sessionStorage`; portal token in `sessionStorage`.
- **Database**: SQL Server only (project rule). 27 EF migrations; `database/scripts` has 20 SQL scripts (see AUD-031). Tenant isolation by global query filter on `IOrgScoped`; soft delete by `ISoftDelete`; `RowVersion` on `OrgEntity`.
- **Auth**: JWT bearer (HS256, 5 min access token, 30 day persistent / 12 h session refresh), TOTP 2FA, lockout (5 attempts / 15 min), password reset, workspace switch, tenant and vendor portals (email one-time code, portal-only JWT).
- **Realtime / jobs**: SignalR `NotificationsHub` ([Authorize], per-user group); `ScheduledJobRunner` hosted service (interval 360 min).
- **CI/CD**: `.github/workflows/ci.yml` (backend build + test, frontend `npm ci` + `ng build`), `cicd.yml`; deploy scripts in `deploy/`. Not run in this audit.
- **Tooling**: `tools/qa-smoke.mjs`, `smoke-i18n.mjs`, `smoke-rtl.mjs`, `seed-demo.mjs` (need a running app; not executed).

## 3. Module and feature inventory (with review status)

Status key: R = code read in detail; S = scanned heuristically; T = has tests; U = unverified at runtime.

| Module | Pages (routes) | Endpoints | Tests | Review |
|---|---|---|---|---|
| Core platform: auth, onboarding, session, menu | login, forgot/reset password, onboarding, account-security, modules | 12 auth + 6 platform + 2 session | ApiTests, TwoFactorTests | R (auth, Program.cs) |
| Core master data: parties, items, CoA, tax codes, currencies, legal entities, BUs | parties, party-detail, items, accounts(+coa), tax-codes, legal-entities, business-units | 25 + 8 | AccountTree, Numbering, ApiTests | S |
| Finance: journals, invoices, receipts, bills, payments, cheques, bank, budgets, reports | 20+ admin pages | 15 + 18 + 16 + 9 + 9 + 6 | Ledger, Receivables, Payables, Cheque, Bank, Budget, Reports tests | S |
| Security/admin: users, roles, audit, settings, custom fields, imports, approvals, messages, documents | 14 pages | 10 + 8 + 3 + 20 | ImportTests, DocumentVault, TwoFactor | R (attachments, imports, custom fields, approvals handlers) |
| Print / PDF | print.page.ts (1,618 lines) | 5 | none | S |
| Dashboard / search / notifications | dashboard (1,610 lines), global-search, notification drawer | 2 + workflow | DashboardTests, SearchAndNotificationTests | S |
| Tenant and vendor portals | portal-login, portal-home, vendor-home | 9 | TenantPortalTests, VendorPortalTests | S |
| Real Estate | ~20 pages | 90 | RealEstateTests (24) + 3 integration files | S |
| HR | 8 pages | 30 | HrTests, HrApiTests, HrExtrasTests | S |
| Inventory | warehouses, stock, movements (3 pages) | module routes | InventoryTests (3) | R (stock posting) |
| Sales | 5 pages | 16 | unit 4, no integration | R |
| Purchasing | 5 pages | 19 | unit 5, no integration | R |
| POS | 5 pages | 15 | unit 4 (34 references), no integration | R |
| Retail | 4 pages | 17 | unit 4, no integration | S + partial R |
| CRM | 4 pages | 16 | unit 5, integration 3 | R (lead convert) |
| Projects | 6 pages | 26 | unit 5, no integration | R (billing) |
| Services | 4 pages | 16 | unit 6, no integration | R (milestones, retainers) |
| Distribution | 4 pages | 16 | unit 4, no integration | R (trips) |
| Manufacturing | 4 pages | 14 | unit 4, no integration | R (work orders) |

## 4. Module-by-module findings

Module-specific page and workflow findings are in `docs/AUDIT-DEEP-2026-10-25.md` sections B and C. Status at the new HEAD for the most important ones is in section 11 below.

## 5. UI/UX defects and improvement opportunities

| ID | Sev | Status | Finding | Evidence | Fix | Acceptance |
|---|---|---|---|---|---|---|
| AUD-001 | High | Verified | **New-module pages are not responsive:** 0 `@media` rules in the 9 new module page folders (11 in the whole app, 9 files). Wide forms and tables break on tablet/phone. | `grep @media pages/{sales,pos,crm,...}` returns 0 | Use ng-zorro grid breakpoints and the shared page layout; test at 375/768 px | No horizontal page scroll at 375 px on every module route |
| AUD-002 | High | Verified | **No i18n in 9 modules** (0 translate usages) and unused `TranslatePipe`/`RevealDirective` imports in 20+ core pages (ng build TS-998113), i.e. dictionaries exist but are not applied. | `ng build` output; scan | Replace literals with `translate` and add EN/AR keys; remove dead imports | `smoke-i18n.mjs` passes for all routes |
| AUD-003 | Medium | Verified | **Accessibility is thin:** 59 `aria-*`/`role` attributes across 185 files; icon-only buttons rely on `title`; no skip link or focus management on drawers/modals confirmed. | grep | Add `aria-label` to icon buttons, focus trap/return, contrast review against `docs/team/uiux/10-responsive-accessibility.md` | axe scan has no serious violations on 10 key screens |
| AUD-004 | Medium | Verified | **Missing loading/error/empty states** on many module pages and project detail (no error handler at all). | scan (`load0`, `err0`) | Use shared skeleton and error panel in `paged-table`/detail layout | Each page shows loading, empty, error |
| AUD-005 | Medium | Verified | **Monolithic components:** `print.page.ts` 1,618, `dashboard.page.ts` 1,610, `shell.component.ts` 1,366, `project-detail` 869, `login` 841, `pos-terminal` 839 lines; shell style budget exceeded (+4.4 kB), initial bundle 1.68 MB vs 1.50 MB budget. | `ng build` | Split by feature, lazy-load charts (apexcharts), trim shell styles | Build has no budget warnings |
| AUD-006 | Medium | Verified | **Hardcoded AED / 5% VAT / `$`:** 55 occurrences of `'AED'`, `AED `, `* 0.05`, `taxRate: 5`, `* 1.05` remain in pages. | grep | Take currency and tax from workspace settings, item tax code and country pack | No currency literal in module pages |
| AUD-007 | Low | Verified | Remaining placeholder ids in UI: CRM interactions `relatedEntityId` uses the Swagger sample GUID; POS, Distribution and Services fall back to `00000000-…0001` when no user is available; Services analytics keeps a fake `currentLegalEntityId`. | `crm/interactions.page.ts:304`, `services-analytics.page.ts:167`, `pos-terminal.page.ts:828` | Remove fallbacks; fail with a message | No all-zero or sample GUID in source |
| AUD-008 | Low | Verified | Frontend has 1 spec file (2 tests). | `find *.spec.ts` | See AUD-040 | |

## 6. API and backend findings

| ID | Sev | Status | Module | Finding | Evidence | Fix | Acceptance |
|---|---|---|---|---|---|---|---|
| AUD-010 | High | Verified | Cross-cutting | **Catch-all exception mapping hides bugs.** Middleware now maps every `InvalidOperationException` to 409 and `ArgumentException` to 400 and returns the message. Framework and programmer errors (for example "Sequence contains no elements") become 409 with internal text, and 500s are never logged for them. | `Middleware.cs` diff in `f68192b` | Map only domain exception types; make module handlers throw `NotFoundException`/`ConflictException`/`DomainException`; keep the 500 fallback | A test asserts a thrown `InvalidOperationException` returns 500 |
| AUD-011 | High | Verified | Attachments | **No owner-level authorization.** `attachments.read` lets a user download any attachment in the workspace by id regardless of the owning record's permission (lease, employee, payroll). `OwnerType`/`OwnerId` are not validated against a real entity. Download uses the client-supplied `ContentType`. | `AttachmentFeatures.cs:63-83`, `WorkflowEndpoints.cs:70` | Resolve owner via an `IAttachmentOwnerResolver` per type and require that type's read/write permission; derive content type from the extension, serve `Content-Disposition: attachment` | Test: user without `hr.employees.read` gets 403 on an employee attachment |
| AUD-012 | Medium | Probable | Auth | **Portal tokens reach authenticated-only core endpoints.** Portal JWTs carry the `Org` claim and pass `.RequireAuthorization()`. Endpoints guarded only by authentication (for example `POST /approvals`, `/notifications`, `/dashboard`, `/search`, `/custom-fields/form/*`, `GET /imports`, `/auth/change-password`, `/auth/2fa/*`) are reachable by a tenant or vendor token. Handlers filter by permission, so data exposure is likely nil, but `POST /approvals` and notification calls are write paths. | `Program.cs:96-98,193-202`; `PortalAuth.cs:66` | Set a fallback policy that rejects `portal_party`, and apply the `portal` policy only to `/portal/*` | Test: portal token gets 403 on `POST /api/v1/approvals` |
| AUD-013 | Medium | Verified | Platform | **No ETag / If-Match (AGENTS.md rule 9).** `RowVersion` exists and `DbUpdateConcurrencyException` maps to 409, but no endpoint exposes or requires the version, so lost updates are only caught inside a single request. | grep `ETag` returns none | Return `ETag` from GET, require `If-Match` on PUT, return 412 | Two-tab edit test returns 412 |
| AUD-014 | Medium | Verified | Platform | **Rate limiting only on login/portal/onboarding.** No global or per-user limiter; expensive endpoints (search, reports, imports, print) are unthrottled. | `Program.cs:101-113` | Add a global token-bucket limiter keyed by user/IP and tighter limits for imports and exports | 429 after burst in a test |
| AUD-015 | Medium | Verified | Platform | **Token revocation uses `IDistributedMemoryCache`.** A restart or second instance forgets revoked `jti`s until the 5-minute expiry. | `Program.cs:63,88` | Redis or database-backed deny list when scaling out | Revoked token rejected after restart |
| AUD-016 | Medium | Verified | Platform | **Idempotency missing** for POS order, goods receipt, work order completion, trip reconcile (double submit posts twice). Milestone invoice and order-to-invoice are idempotent through stored ids. | handlers | `Idempotency-Key` header stored per user and route | Replay returns the first result |
| AUD-017 | Medium | Verified | Platform | **Postings are not in the document's transaction:** the document is saved, then stock/ledger postings run in separate transactions. A posting failure leaves a saved document with no stock/journal. | POS, Sales, Purchasing, Manufacturing, Distribution handlers | Wrap document + postings in one `InTransactionAsync` or an outbox | Fault-injection test rolls back the document |
| AUD-018 | Low | Verified | Platform | No API versioning beyond `/api/v1`, no OpenAPI response annotations (`Produces`), Swagger only in Development. | Program.cs | Add `ProducesProblem`/response types | Generated client compiles |
| AUD-019 | Low | Verified | Platform | Single `/health` endpoint (database only); no readiness/liveness split, no storage or job health. | Program.cs:205 | Separate `/health/live` and `/health/ready` | |
| AUD-020 | Low | Verified | Platform | `ERP.Api/ProgramSeeder.cs` is 3,188 lines in the host project, references every module domain and uses `AppDbContext` directly, against the layering rule that only Infrastructure touches the context. Gated by `Seed:Enabled` (false by default). | `ProgramSeeder.cs` | Move to a `DemoData` project behind an interface | Architecture test covers the host |

## 7. Database and data-integrity findings

| ID | Sev | Status | Finding | Evidence | Fix | Acceptance |
|---|---|---|---|---|---|---|
| AUD-030 | High | Verified | **Cross-module references have no foreign keys.** New-module tables store `CustomerPartyId`, `VendorPartyId`, `ItemId`, `ClientId`, `WarehouseId`, `LegalEntityId` as bare GUIDs. Counts of `HasForeignKey` in module models: CRM 0, Services 0, Retail 1, Sales 2, Distribution 2, Manufacturing 2 (internal parent/child only). Orphaned references are possible (and the placeholder GUIDs in the UI show it). | module `*Module.cs` | Add FKs with `Restrict` to Party, Item, LegalEntity, Warehouse (new migration; needs owner approval) | Insert with unknown party fails at the database |
| AUD-031 | Medium | Verified | **SQL scripts lag migrations:** 20 scripts in `database/scripts` vs 27 migrations; scripts 0021-0027 (finance hardening, `ModulesExpansion`) are absent although the handoff says scripts match migrations. | `ls database/scripts` | Generate idempotent scripts per migration in CI | CI diff check fails when a migration has no script |
| AUD-032 | Medium | Verified | **One migration holds nine modules (41 tables)**, so a failed deploy cannot be rolled back per module. No seed data for them besides demo seeding. | `20261024071544_ModulesExpansion` | Split going forward; test `Down()` | |
| AUD-033 | Medium | Verified | **Custom-field JSON column check (`ISJSON`) missing** in all nine new modules (rule 8). | grep `ISJSON` in module models: 0 | Add check constraints | |
| AUD-034 | Medium | Probable | **Stock cost replay is O(history).** `StockCosting.Replay` loads every movement for item+warehouse on each issue, called once per line, sequentially. Large histories make POS and delivery slow, and concurrent issues can compute the same average cost (no lock/serializable). | `StockPostingService.cs` | Maintain a stock balance table updated in the same transaction, or snapshot per period | 1,000-line order issues within a set time; parallel issues stay consistent |
| AUD-035 | Medium | Verified | Child tables (`InvoiceLine`, `BillLine`, `JournalLine`, line items in modules) are not `IOrgScoped`; isolation relies on always loading through the parent. Direct queries on child sets would cross tenants. | Domain classes | Add `OrgId` to child tables or forbid child `DbSet` queries by architecture test | |
| AUD-036 | Low | Verified | Only 6 uses of `AsNoTracking`/`AsSplitQuery` in `backend/src`; module queries rely on repository defaults. | grep | Audit read paths | |
| AUD-037 | Info | Verified | EF model is in sync with migrations (`has-pending-model-changes`: none). | command result in section 11 | | |

## 8. Security and permission findings

| ID | Sev | Status | Finding | Evidence | Fix | Acceptance |
|---|---|---|---|---|---|---|
| AUD-050 | High | Verified | **Runtime frontend dependency vulnerabilities.** `npm audit --omit=dev`: 10 (5 high, 5 moderate), all in `@angular/*` 19.2 (XSS via i18n attributes, sanitization bypass via host bindings, SSR hydration cache poisoning). `npm audit` overall: 35 (2 critical `piscina`/`tar`, 18 high), mostly dev tooling (vite, webpack-dev-middleware, launch-editor). | `npm audit` | Upgrade Angular to the latest patched 19.x line (verify the advisory ranges) and `@angular/cli`/build packages; add `npm audit --omit=dev` to CI | Prod audit has 0 high |
| AUD-051 | Medium | Verified | **Backend: `SQLitePCLRaw.lib.e_sqlite3 2.1.6` high advisory (GHSA-2m69-gcr7-jv3q)** in RealEstate and Inventory dependency graphs (transitive). SQLite is also contrary to the "SQL Server only" rule; `erp-dev.db*` files exist under `ERP.Api` (git-ignored). | `dotnet list package --vulnerable` | Remove the SQLite profile or update the package | |
| AUD-052 | Medium | Verified | **Dev credentials committed:** `appsettings.Development.json` holds a dev signing key and a dev admin password (values not reproduced). Violates "no secrets in repo". Base `appsettings.json` correctly has an empty signing key and startup fails if it is shorter than 32 bytes. | `appsettings.Development.json` | Move to user-secrets; rotate; keep placeholders | |
| AUD-053 | Medium | Verified | **No `Content-Security-Policy`** header; only nosniff, no-referrer, frame DENY, HSTS outside dev. With Angular runtime advisories (AUD-050), a CSP is a needed second layer. | `Program.cs:161-168` | Add CSP, `Permissions-Policy`, COOP | Header present on SPA responses |
| AUD-054 | Medium | Verified | **`LocalFileStore.PathFor` uses `StartsWith(Root)` without a trailing separator** (a sibling folder such as `storage2` would pass). Keys are server-generated so exploitation is unlikely, but the check is wrong. | `CoreServices.cs:121-125` | Compare with `Root + Path.DirectorySeparatorChar` | Unit test with sibling path |
| AUD-055 | Medium | Verified | **UI permission gating absent in new modules**, so users see actions they cannot perform. Backend enforces (every new-module endpoint has `RequirePermission` or a permission parameter: counts match endpoints). | scan; endpoint counts | `*erpHasPermission` on actions | |
| AUD-056 | Low | Verified | **Wildcard `AllowedHosts: *`** and CORS origin default `localhost:4200` must be overridden per environment. | appsettings | Set per environment; document | |
| AUD-057 | Info | Verified | Positive findings: HS256 pinned (`ValidAlgorithms`), `MapInboundClaims=false`, 30 s skew, query-string token only for `/hubs`, lockout and generic portal responses, TOTP, refresh rotation, deny-list on logout, SignalR groups per user, SPA fallback excludes `/api`, `/hubs`, `/swagger`. | `Program.cs`, `NotificationsHub.cs` | | |

SQL injection: no raw SQL concatenation was found in the files read (EF Core LINQ throughout); the paging/sorting layer uses whitelisted fields. XSS: Angular templates were not reviewed for `innerHTML`/`bypassSecurityTrust*` (not verified). CSRF: bearer-token API with refresh via cookie; refresh and logout endpoints should require a custom header or SameSite=Strict, not verified.

## 9. Performance and scalability findings

| ID | Sev | Status | Finding |
|---|---|---|---|
| AUD-060 | Medium | Probable | Stock issue replay (AUD-034). |
| AUD-061 | Medium | Probable | Postings run per line sequentially (`foreach … await IssueStockAsync`) in POS, Sales, Manufacturing, Distribution: N database round trips and N transactions per document. Needs profiling. |
| AUD-062 | Low | Verified | Initial JS bundle 1.68 MB; apexcharts and ng-zorro loaded eagerly (AUD-005). |
| AUD-063 | Low | Probable | POS catalog loads the first 100 items only and filters client-side; large catalogs will be truncated, and every keypress re-filters. |
| AUD-064 | Low | Unverified | Dashboard fans out several queries per load; no caching layer beyond in-memory distributed cache; needs profiling. |
| AUD-065 | Low | Verified | Background jobs run in the API process (`Jobs:Enabled` default true); a multi-instance deployment would run each job on every instance. Needs a distributed lock. |

## 10. Frontend-backend integration gaps

Route-string cross-check (nine new modules, run at the previous HEAD): all list/create/get calls match backend routes; mismatches were inherited CRUD calls with no backend route (CRM leads delete, opportunities PUT/DELETE, interactions GET/PUT/DELETE; Distribution trips PUT/DELETE; Manufacturing work orders PUT/DELETE; POS sessions CRUD and orders PUT/DELETE; Purchasing receipts PUT; Services engagements/retainers/milestones/time-entries PUT/DELETE and milestone/time-entry GET). Services time entries have an API and no UI. Sales and Purchasing forms did not send party/item ids (fixed in part by `f68192b`, see section 11). Traceability for the nine modules is: UI page → `core/api/<module>.api.ts` → `/api/v1/m/<key>/…` → MediatR handler → repository → table; verified for create paths of Sales, Purchasing, POS, Services milestones, Projects billing; not verified at runtime for the rest.

Traceability gap: no integration test exists for any of Sales, Purchasing, POS, Retail, Distribution, Manufacturing, Projects, Services endpoints. Only validators and CRM leads are tested.

## 11. Build, test and verification results

| Check | Command | Result |
|---|---|---|
| Git state | `git status -sb`, `git fetch --all --prune`, `git rev-list --left-right --count HEAD...origin/main` | Clean tree. Local branch 0 ahead / 2 behind `origin/main` (two merge commits). Fast-forwarded with `git merge --ff-only origin/main`; no local work lost. Now 2 ahead of its own remote branch tip only because of that fast-forward. |
| Backend build | `dotnet build backend/ERP.sln` (clean export in scratch, because the user's running API holds DLL locks in the working tree) | 0 errors, 6 warnings, 46 s |
| Backend tests | `dotnet test backend/ERP.sln --no-build` | 212/212 unit, 167/167 integration passed |
| Vulnerable packages | `dotnet list … package --vulnerable --include-transitive` | 1 high (`SQLitePCLRaw.lib.e_sqlite3 2.1.6`) |
| Deprecated packages | `dotnet list … package --deprecated` | `xunit 2.9.2` legacy |
| EF model vs migrations | `dotnet ef migrations has-pending-model-changes` | No pending changes |
| Frontend build | `npx ng build` | Succeeds; warnings: initial bundle +183.78 kB over 1.50 MB budget, shell styles +4.37 kB over 16 kB, `papaparse` not ESM, NG8102 x3, 30+ TS-998113 unused imports |
| Frontend unit tests | `npx ng test --watch=false --browsers=ChromeHeadless` | 2 of 2 passed |
| Frontend audit | `npm audit`, `npm audit --omit=dev` | 35 total (2 critical dev-tooling, 18 high); production deps 10 (5 high) |
| Lint / static analysis | none configured (no ESLint, no `dotnet format` gate) | Not run; gap AUD-041 |
| API contract / E2E / browser UI | `tools/qa-smoke.mjs` etc. require a running API and database | **Not run** |
| Load / performance profiling | none | **Not run** |

Status of earlier findings at the new HEAD (by diff and grep, not by running):
- Fixed or improved: HTTP 500 for `KeyNotFound`/`InvalidOperation` (now 404/409, but see AUD-010); stock receive credit moved to an `inventory.clearing` account with fallbacks (B1, partly); cross-entity warehouse fallback closed (B6, partly); Projects billing now creates an invoice; Services `/milestones/{id}/invoice` exists; Sales/Purchasing forms received pickers; POS cashier now uses the signed-in user with a placeholder fallback.
- Still open (needs re-verification by the implementing agent): B2 manufacturing journal design, B3 work-order status guards, B4/B5 POS void/payments/revenue posting, B7 fulfillment returns, B8 trip reconcile invoicing, A6 status state machine, A7 transactional postings (AUD-017), A8 ETag (AUD-013), A4 hardcoded currency and tax (AUD-006), A2/A3 (AUD-055, AUD-002).

## 12. Missing, incomplete, incorrect and redundant functionality

Essential (production blockers): finance and stock integration correctness for the new modules (docs/AUDIT-DEEP B1-B11), document state machines, POS server-side pricing/tax/payment validation, owner-level attachment authorization (AUD-011), integration tests for every module, UI permission gating, i18n, FK constraints.
Important: edit/delete screens and routes listed in AUDIT-DEEP section C and D.1; Services time entry UI; retainer billing run; stock receive/issue/adjust/transfer screens; project timesheet approval; CRM lead to customer to quote linkage; master-data soft delete UI (Parties, Items, Tax codes, Legal entities, Business units, Accounts have none).
Redundant or low-value: duplicate paged-list wiring with `any` in every module page; `Inventory` stub entity; unused imports in 30+ components; two audit documents with overlapping content (merge after fixes).
Business decisions needed (not assumed): tax model for POS (inclusive/exclusive, multi-rate), accounts for GR/IR and WIP, whether sales return/credit flows are in scope, approval requirement for POs, who may void POS orders, timesheet approval policy.

## 13. Technical debt and architectural weaknesses

Testing and tooling findings:

| ID | Sev | Status | Finding | Fix | Acceptance |
|---|---|---|---|---|---|
| AUD-040 | High | Verified | No integration tests for Sales, Purchasing, POS, Retail, Distribution, Manufacturing, Projects, Services; frontend has 2 unit tests and no component or E2E tests in CI. The accounting and stock flows added recently are untested. | One integration test class per module covering create, state change, finance/stock effect, permission denial and tenant isolation; Karma specs for shared components; Playwright smoke in CI | CI fails when a module has no integration tests |
| AUD-041 | Medium | Verified | CI (`ci.yml`) runs only build and backend tests: no lint, format check, `npm audit`, `dotnet list package --vulnerable`, frontend tests, or migration script check. | Add those steps | CI red on a known vulnerable package |

- Host project contains a 3,188-line seeder and direct `DbContext` use (AUD-020).
- Handlers across modules duplicate document patterns (number generation, status change, line totals); a shared document base and line-calculation service would remove hundreds of lines and make fixes land once.
- Frontend: module pages copy 400-500 lines of list + drawer + form code each; a generic CRUD page/record-drawer (already present for core master data) should be reused.
- Only generated `any` typing at the API boundary; no generated API client.
- Exceptions: three ways to signal errors (domain exceptions, framework exceptions, ProblemDetails) before the recent middleware change.
- No lint, formatting or analyzer gates; no dependency update automation.

## 14. Recommended improvements (optional, after essentials)

Generated TypeScript client from OpenAPI; outbox for postings; Redis for cache/deny-list/job lock; Playwright E2E per module (the repo already has `docs/team/sqa/playwright`); barcode scanning and held sales in POS; inventory lots/serials and valuation report; project profitability; CRM-to-quote conversion; audit-log retention; Dependabot/Renovate; OpenTelemetry traces and a Serilog sink for files/Seq.

## 15. Prioritized roadmap

- **P0 (before any production use):** AUD-011 attachment authorization; POS/Manufacturing/Purchasing accounting corrections (AUDIT-DEEP B1-B5, B9, B10); AUD-017 transactional postings and AUD-016 idempotency; state-machine guards (A6); AUD-050 Angular security upgrade; AUD-030 foreign keys (with owner approval for migrations); integration tests proving the above. Verification: new integration tests green, trial balance still zero-sum after each flow.
- **P1:** AUD-010 exception mapping cleanup; AUD-012 portal token fallback policy; AUD-013 ETag/If-Match; AUD-006 currency/tax from settings; AUD-055 UI permission gating; missing edit/delete screens and routes; Services time entries; AUD-031 SQL scripts; AUD-033 JSON checks. Dependencies: P0 data model decisions.
- **P2:** AUD-001 responsive pass; AUD-002 i18n; AUD-003 a11y; AUD-004 states; AUD-014 rate limits; AUD-015 deny-list store; AUD-034 stock balance table; AUD-053 CSP; lint/format/audit gates in CI (AUD-041).
- **P3:** bundle budgets and component splits (AUD-005); advanced features in section 14.

## 16. Files and components affected

Per finding in the tables above. Highest-traffic files: `backend/src/ERP.Api/Infrastructure/Middleware.cs`, `Program.cs`, `ERP.Application/Workflow/AttachmentFeatures.cs`, `ERP.Modules.Inventory/Features/StockPostingService.cs`, every `*/Features/OrderFeatures.cs` / `WorkOrderFeatures.cs` / `BillingFeatures.cs` / `MilestoneFeatures.cs` / `ReceiptFeatures.cs` / `TripFeatures.cs`, `frontend/src/app/pages/{sales,purchasing,pos,crm,projects,retail,services,distribution,manufacturing}/*`, `frontend/package.json`, `database/scripts`.

## 17. Evidence and reproduction

Reproduction for each item is the evidence column plus: for static findings, open the cited file and line; for AUD-011 call `GET /api/v1/attachments/{id}/download` with a token that has `core.attachments.read` but not the owner's permission (needs runtime); for AUD-012 call `POST /api/v1/approvals` with a portal token; for AUD-050 run `npm audit --omit=dev` in `frontend`; for AUD-031 compare `ls database/scripts` with `Migrations`.

## 18. Unverified areas and limits of this audit

- **Runtime:** the application was not started; no browser testing, no E2E, no load or profiling. AUD-012, AUD-034, AUD-061 and all UI layout items need a running environment (SQL Server, seeded workspace, test accounts).
- **Read in detail:** `Program.cs`, middleware, behaviors, attachments, imports, custom fields, approvals, notifications handlers, file store, stock posting, POS orders/sessions, Sales orders/quotes, Purchasing orders/receipts, Projects billing, Services milestones, Manufacturing work orders, Distribution trips, CRM lead convert, module endpoint registration, migrations/model sync.
- **Scanned only (heuristics):** all remaining pages and components; Real Estate, HR, finance documents, print, dashboard, portals.
- **Not reviewed:** individual finance posting rules and reports math, country-pack tax logic, email/SMTP flow, 2FA recovery code handling, SignalR payloads, deploy scripts and Cloudflare tunnel config, Dockerfile, database indexes against real query plans, PDF/print output, every Angular template for `innerHTML`/`bypassSecurityTrust*`, CSRF posture of the refresh cookie.
- **Counts** of endpoints and tests come from regex scans and may be off by a few percent.
- The other agent is committing fixes while this audit ran, so statuses in section 11 can change within hours.

Not "100% audited". Coverage: core platform and security plumbing: reviewed; nine newer modules: reviewed in depth on money, stock and state flows, partially on UI; older modules: partially reviewed; runtime behaviour: unverified.

Remaining work for full verification: start the stack against a throwaway SQL Server database, run `tools/qa-smoke.mjs` and the Playwright cases in `docs/team/sqa/playwright`, add the missing integration tests, run axe and Lighthouse on the top 15 screens, profile the stock and dashboard paths, and review the unscanned modules' handlers.
