# Test Strategy — ERP Core + Real Estate module (Sprint 0 onward)

Owner: SQA · Version 0.3 · 22 Oct 2026 (PKT, UTC+5) · v0.2/v0.3 apply the coordinator's decisions (see §14, Status column)
Source of truth: `/workspace/erp-team/spec.txt` (the reference demo Complete Build Spec v1.0). Section references (§) point to that document.
Where the spec is silent, assumptions are marked **[ASSUMPTION]** and listed in §14 so the BA and dev teams can confirm or override them.

---

## 1. Scope and quality goals

In scope from day one:

| Area | Spec basis | Why it matters most |
|---|---|---|
| Tenant (organization) isolation | §2.2, §7.3, §10.3: `OrgId` on every row, EF global filter, org taken from the `org` claim and never from the body | A cross-org leak ends a multi-tenant SaaS product. Severity is always S1. |
| Legal-entity scoping | §2.2, §4.30, §7.3: `ents` claim, `X-Entity-Id` header, second global filter on `LegalEntityId` | Each LLC keeps separate books. A leak is a confidentiality and tax problem. |
| AuthN: login, workspace selection, refresh, logout, password reset | §4.1, §7.3, §10.4 + DECIDED: **5-min** JWT in memory with a `jti` denylist on logout; rotating refresh token in an HttpOnly Secure SameSite=Strict cookie (30 days with "Keep me signed in", otherwise session-only); lockout 5 / 15 min; multi-workspace picker; single-use 30-min reset tokens; password policy ≥ 10 + upper/lower/digit | Sprint 1 scope (§13). |
| AuthZ: permission policies | §7.1–7.2 grants, re-keyed to the DECIDED `<module>.<resource>.<action>` format (§14b); 9 built-in roles (RE roles from the RE template) | Sprint 1 scope. |
| Segregation of duties | §5.8, §7.3: a decider can never be the requester | Financial control. |
| Module enable/disable (ERP core) | **Not in the spec.** It comes from the ERP product direction (shared core + pluggable industry modules). See §14 A-M1…A-M8. | It gates menus, routes, APIs and jobs. |
| Ledger invariants, period lock | §5.9, §5.10, §10.5 | From Sprint 3. Listed here so the pyramid is sized for them. |

Quality goals: zero known cross-org or cross-entity leaks at any release; every API endpoint has an authorization test; every posting rule is covered by a balancing test (Σ debit = Σ credit); report totals match the appendix seed values (§13.2, for example receivables 155,362.50).

## 1a. Principle: industry-agnostic core (hard constraint from Muhammad)

The product is a general-purpose ERP. Real estate is **one** industry module, not the product. QA enforces this in four ways:

1. **Core stands alone.** The core (organization/tenant setup, legal entities, users, roles, permissions, auth, settings, approvals engine, audit, numbering, documents, notifications, general ledger) must build, migrate, seed and pass its full test suite with **zero industry modules enabled or even loaded**.
2. **One-way dependencies.** Modules depend on core contracts (`Core.Abstractions`: `IModule`, `IPermissionRegistry`, `IMenuRegistry`, `IPostingService`…). Core never references a module assembly, type, table or enum. This is enforced by architecture tests (NetArchTest.Rules or ArchUnitNET), for example:
   ```csharp
   [Fact, Trait("Category","Architecture")]
   public void Core_must_not_depend_on_any_module() =>
       Types.InAssemblies(CoreAssemblies)                 // Domain, Application, Infrastructure, Api (core)
            .ShouldNot().HaveDependencyOnAny("ERP.Modules.*", "reference-demo.Modules.*")
            .GetResult().IsSuccessful.Should().BeTrue();
   ```
   Equivalent checks for Angular: an ESLint `no-restricted-imports` / Nx `enforce-module-boundaries` rule saying `core/**` and `shared/**` may not import `modules/**`.
3. **No industry vocabulary in core.** Core schema, seed data, i18n labels, default navigation and core API DTOs contain no real-estate terms (cheques/PDCs, invoices, bills, receipts and payments are core finance and allowed; property, unit, lease, tenant-as-lessee, Ejari, Tawtheeq, brokerage, rent…). A "vocabulary lint" test scans core migrations, the core seed, `core` i18n JSON and the core OpenAPI document against a denylist (`/sqa/core-vocabulary-denylist.txt`, maintained by SQA). Each module contributes its own permissions, menu items, routes, tables (own schema, e.g. `re.`), jobs and seed data via registration.
4. **Module add/remove needs no core migration.** Module tables live in a module-owned migrations assembly/history table (e.g. `__EFMigrationsHistory_RealEstate`). Installing, enabling, disabling or removing a module leaves the core migration history and core schema hash unchanged.

Test assets that support this: a **Dummy/Sample module** (`ERP.Modules.Sample`, test-only: one entity `SampleItem`, permissions `sample.items.read/write`, one menu item, one route, one Hangfire job) used to prove the plug-in contract without real-estate logic; and an **onboarding industry matrix** (real estate, retail, manufacturing, services, generic/no industry), run as parameterized API tests and Playwright E2E (`playwright/tests/onboarding.industries.spec.ts`).

---

## 2. Test pyramid

| Layer | Target share of test count | Runs in | Typical runtime budget | What it covers |
|---|---|---|---|---|
| **Unit: Domain** (`reference-demo.Domain.Tests`) | ~45% | every PR | < 30 s | Value objects (Money, DateRange), state machines (LeaseStatus, ChequeStatus…), rent schedule maths, posting rule templates, validators (TRN 15, IBAN AE+21, Emirates ID, MOHRE 13/14, routing 9). |
| **Unit: Application** (`reference-demo.Application.Tests`) | ~20% | every PR | < 60 s | MediatR handlers, the authorization behaviour, validation behaviour, approval service (decider ≠ requester), permission evaluation, module-gate evaluator, token service (claim building, `ver`). Use fakes for `ICurrentUser`, `IClock`, `IAppDbContext`. |
| **Integration / API** (`reference-demo.Api.IntegrationTests`) | ~20% | every PR | < 8 min | `WebApplicationFactory<Program>` + **Testcontainers SQL Server** (real engine, so global filters, filtered unique indexes, rowversion and the period-lock trigger all behave as in production). Tenant isolation, entity scoping, permission matrix, auth flows, module gating, ProblemDetails contract. |
| **Component: Angular** | ~12% | every PR | < 3 min | Jest + Angular Testing Library: LoginComponent, auth interceptor (bearer, silent refresh on 401, `X-Entity-Id`), route guards, `*hasPermission` directive, entity switcher, module-gated sidebar, form validators. |
| **E2E: Playwright** (`/sqa/playwright`, later `tests/e2e`) | ~3% | PR smoke (login), full suite nightly and pre-release | smoke < 3 min; full < 25 min | Critical journeys only: login/refresh/logout, hard reload of a deep URL (§8 #1), entity switch, module toggle visible effects; later lease-to-cash and bill-to-payment (§13.2). |

Rule of thumb: push a check to the lowest layer that can prove it. A permission or isolation rule is **proven at the API layer** (UI hiding is UX only, §7.3: "hide what the API would refuse anyway"). E2E only confirms that the UI is wired up.

Note on §13.2 ("in-memory or SQLite context" for application tests): this is acceptable for pure validation rules only. **Anything about tenant or entity filters, `IgnoreQueryFilters`, unique indexes, concurrency or the lock trigger must run on SQL Server via Testcontainers**, because the InMemory provider enforces no relational constraints and SQLite differs in collation, `rowversion` and filtered indexes.

---

## 3. Tooling

| Purpose | Tool | Notes |
|---|---|---|
| .NET test framework | xUnit v2 (stay on v2 until the team agrees on v3) | `[Trait("Category","Isolation")]` etc. are used for CI filtering. |
| Assertions | FluentAssertions | `BeEquivalentTo` for DTOs. Pin the major version, because the licence changed from v8. **[ASSUMPTION]** Pin 7.x, or switch to Shouldly if the licence is an issue. |
| DB for integration tests | Testcontainers.MsSql (`mcr.microsoft.com/mssql/server:2022-latest`) | One container per test assembly (collection fixture). Apply migrations with `Database.MigrateAsync()`, not `EnsureCreated`, so the migrations themselves are tested. |
| DB reset between tests | Respawn | Reset to the seeded baseline; reference data is kept. |
| API host | `Microsoft.AspNetCore.Mvc.Testing` WebApplicationFactory | Override `IClock` (fixed at 2026-10-22 Asia/Dubai), replace email/SMS/Blob with fakes, run Hangfire jobs in-process synchronously. |
| Mocks | NSubstitute | Prefer fakes for `ICurrentUser`/`IClock`. |
| Test data | Builder pattern (`TestData.Org()`, `.WithEntity()`, `.WithUser(role)`) plus Bogus for non-key fields | Deterministic GUIDs for seeded rows. |
| Angular unit/component | **Recommended: Jest** (`jest-preset-angular`) + `@testing-library/angular` | It matches §13.2 ("Jest or Vitest"). Karma is deprecated by the Angular team, so we do not use Jasmine/Karma. Revisit Vitest once the Angular CLI builder for it is stable. |
| E2E | Playwright (TypeScript) | Chromium on PRs; Chromium, Firefox and WebKit nightly; mobile viewport for §4.32. |
| API contract | OpenAPI snapshot test (Swashbuckle/NSwag JSON diff) | Breaks the build on an unreviewed contract change. |
| Security | OWASP ZAP baseline (nightly, staging); `dotnet list package --vulnerable`; `npm audit --audit-level=high`; gitleaks secret scan; CodeQL or Semgrep | §13.2 names ZAP. |
| Performance | k6 | §13.2 targets: lists < 500 ms p95, reports < 2 s. Runs nightly on staging from Sprint 8. |
| Accessibility | `@axe-core/playwright` | Login and shell from Sprint 1; WCAG 2.1 AA. |
| Coverage | coverlet (+ ReportGenerator); Jest `--coverage` | Diff coverage is posted on the PR. |

---

## 4. Test data and tenant seeding

**Two organizations are always seeded**, because isolation cannot be tested with one.

| Seed | Content | Use |
|---|---|---|
| **Org A: "Harbour & Oasis Group (Demo)"**, sign-in key `demo` | Core seed **+ RE industry template** (RE roles, RE CoA from §4.17, RE settings) + the appendix §14.1 data: ENT-0001 Harbour Assets LLC (Dubai), ENT-0002 Oasis Investments LLC (Abu Dhabi), 4 properties, 20 units, 12 customers, 10 leases, 8 vendors, 3 bank accounts, employees, the CoA from §4.17 | Functional tests, report-total assertions, UAT (§13 sprint 8) |
| **Org B: "Isolation Test Org"**, sign-in key `iso-b` **[ASSUMPTION: name/key]** | A mirror of Org A's **codes** (ENT-0001, PROP-0001, CUS-0001, LSE-2026-000001…) with different names and amounts, one legal entity, and users with the **same email local-parts** under a different domain | Proves that code-based lookups, unique indexes (`OrgId` included, §7.3), search and exports never collide or leak |
| **Multi-workspace user** | `qa.multi@example.test`: Org admin in Org A, Viewer in Org B; `qa.single@…`: Org A only | Workspace picker and switching (WS-*) |
| **QA users in Org A**: one per built-in role (§2.3); RE roles exist after the RE industry template is applied | `qa.orgadmin@`, `qa.finance@`, `qa.accountant@`, `qa.leasing@`, `qa.facilities@`, `qa.hr@`, `qa.brokerage@`, `qa.approver@`, `qa.viewer@` (`@demo.the reference demo.local`) **[ASSUMPTION]** | Permission matrix |
| **Entity-restricted user** in Org A | `qa.leasing.oasis@` with Leasing manager and "Only selected entities" = ENT-0002 | Entity-scoping cases |
| **Inactive user** | `qa.inactive@`, Active (can sign in) = off | Auth cases |
| **Multi-role user** | Accountant + Approver | Union-of-permissions and SoD cases |

Rules:
- Seeds are code (C# builders plus an idempotent `SeedRunner`), not SQL dumps, so they stay in step with migrations.
- Every test that creates data creates it **in both orgs** when it is an isolation test, and asserts on the "other" org.
- Integration tests never depend on run order. Respawn resets after each test class.
- The clock is frozen at **22 Oct 2026, Asia/Dubai** (the snapshot date of the appendix data) so that "overdue", "expires in 60 days" and so on are deterministic.
- Staging and UAT use the same seeder (Org A only, plus Org B on staging for the isolation smoke). Production never runs QA seeds. The seeder refuses to run when `ASPNETCORE_ENVIRONMENT=Production`.
- PII in seeds is synthetic (Emirates ID, IBAN and TRN values are format-valid but fake).

---

## 5. Environments

| Env | Purpose | Data | Deployed by |
|---|---|---|---|
| Local | Dev and QA authoring | Docker compose (§13 sprint 0: SQL Server, Redis, MinIO, Seq) + seeder | developer |
| CI (ephemeral) | PR gates | Testcontainers per run | GitHub Actions / Azure DevOps (§13.3) |
| Staging | Nightly full E2E, ZAP, k6, UAT | Seeded Org A + Org B; reset nightly | auto on `main` (§13.3) |
| Production | Live | Real | manual approval (§13.3) plus the release checklist |

Config parity: the same container image moves through the environments; only configuration differs (secrets from Key Vault or environment variables, §7.3). E2E reads `E2E_BASE_URL` (default `http://localhost:4200`).

---

## 6. Coverage targets

| Layer | Line | Branch | Other |
|---|---|---|---|
| Domain | ≥ 90% | ≥ 85% | 100% of posting rules in §5.10 have a balancing test |
| Application | ≥ 80% | ≥ 75% | Every command and query handler has at least one authorization test |
| API | n/a (measured as an inventory) | — | **100% of endpoints** appear in the endpoint-authorization inventory test (§8), and every endpoint marked entity-scoped has a cross-org and a cross-entity test |
| Infrastructure | ≥ 60% | — | Token service, refresh-token store and tenant resolver at ≥ 90% |
| Angular | ≥ 75% | ≥ 65% | Auth service, interceptors, guards and `hasPermission` at ≥ 90% |
| New code (diff coverage) | ≥ 80% | — | Enforced on the PR |

Coverage is a floor, not a goal. Reviewers reject tests that only assert "no exception".

---

## 7. CI gates (what blocks a merge to `main`)

Required checks on every PR:
1. `dotnet build` with warnings as errors (nullable on) and `dotnet format --verify-no-changes`.
2. Domain + Application unit tests are green.
3. API integration tests on Testcontainers are green, **including the `Isolation` and `Authorization` traits. These can never be quarantined** (see §9).
4. The endpoint-authorization inventory test is green. Every action has `[HasPermission]`, `[AllowAnonymous]` (from an approved allow-list: `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/forgot`, `/auth/reset`, `/auth/reset/validate`, `/auth/password-policy`, health; `/auth/select-workspace` accepts only the pre-auth token) or `[Authorize]` with an explicit "any authenticated" note.
5. The entity-inventory test is green. Every EF entity implementing `IOrgScoped` has the `OrgId` global filter, every `ILegalEntityScoped` entity has the entity filter, and **no `IgnoreQueryFilters()` call exists outside an allow-list** (Roslyn analyzer or reflection test plus a grep step).
6. The EF migration bundle builds, and it applies to an empty DB and to the previous release's schema.
7. Angular: `ng lint`, Jest with coverage thresholds, and `ng build --configuration production` are green.
8. Playwright login smoke (Chromium) is green against the PR build.
9. Diff coverage is ≥ 80%.
10. No high or critical vulnerabilities (`dotnet list package --vulnerable`, `npm audit --audit-level=high`) and no gitleaks findings.
11. The OpenAPI snapshot is unchanged, or the diff was approved in the PR.
12. One approving review; the reviewer is not the author.
13. **Industry-agnostic core gate.** (a) A CI job runs the core unit and integration suites with **zero modules loaded** (`ERP_MODULES=` empty; the host is built without module assemblies) and must be green. (b) Architecture tests (NetArchTest/ArchUnitNET + the Angular boundary lint) prove that no core project references a module assembly. (c) The vocabulary lint is green on core schema, seed, i18n and the OpenAPI document. (d) The core OpenAPI snapshot is identical with no modules, with Real Estate only, and with the Sample module only (module endpoints live under `/api/v1/m/{moduleKey}/…` **[ASSUMPTION]** so they never alter core paths). (e) The core migration history and schema hash are unchanged after module install, enable, disable and uninstall. (f) The old flat module paths (`/properties`, `/leases`, `/invoices`…) return 404 (CORE-030). (g) The permission registry test passes: every key is `<module>.<resource>.<action>` and module keys use their module's prefix (PERM-030/036).

Nightly (they do not block a PR, but they **block a release** while red): full Playwright cross-browser and mobile, ZAP baseline, k6 (from Sprint 8), axe, and the full report-total suite.

---

## 8. Security testing approach

- **Automated in the integration suite** (every PR): cross-org and cross-entity reads, writes and IDOR; permission matrix (`/sqa/permission-matrix-cases.csv`); JWT tampering (`alg:none`, HS/RS confusion, bad signature, expired, wrong issuer or audience, edited `org`/`perms`/`ents` claims); refresh rotation and reuse detection; rate limiting (429 with "Too many attempts. Please wait a minute and try again.", §4.1); lockout; SoD.
- **Inventory tests** (above) stop a "forgot the attribute" or "forgot the filter" class of bug.
- **Angular**: no tokens in `localStorage`/`sessionStorage` (unit test on AuthService plus an E2E storage check); CSP is respected (no inline script); `innerHTML`/`bypassSecurityTrust*` is banned by a lint rule.
- **Headers** (integration): HSTS, CSP, `X-Content-Type-Options`, `Referrer-Policy`, and refresh-cookie flags `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth`.
- **DAST**: ZAP baseline nightly on staging; a ZAP full or authenticated scan before each release; an external pen test before v1.0 (§13 sprint 14).
- **Uploads** (from Sprint 2): type and size limit (10 MB, §4.25), AV scan, SAS URLs valid 5 minutes (§10.7).
- **Sensitive fields** (DECIDED): Emirates ID, passport and IBAN are masked for users without `core.sensitive.read`; salary is gated by the HR module's `hr.payroll.read` and stays hidden while HR is disabled, on every surface (UI, API, export, search, audit diff). Salary visibility follows the HR module permissions (G-PERM-4).
- **Token revocation** (DECIDED): logout puts the access token's `jti` in a shared denylist (Redis) checked on every request; the test suite runs two API instances to prove it.
- **Severity**: any cross-org leak is S1/P0, a stop-ship and a hotfix. A cross-entity leak within an org is S1. An authz bypass is S1.

## 8a. Custom fields: test approach

The spec basis is §9: "custom fields (text, number, date, select) on properties, units, customers, leases, vendors, employees; usable in filters and exports". The new requirement extends this with per-org definitions, values in a JSON column, and more types; the BA's detailed rules are in `ba/core-domain.md` §8 and the test cases follow them. To stay industry-agnostic, the custom-field engine is **core** and works on any entity that opts in (`IHasCustomFields`); the RE entities and the Sample module entity are just consumers.

| Layer | What is tested |
|---|---|
| Domain unit | Per-type validators (length, range, regex, precision, options), key format, required on create vs update, JSON (de)serialization round trip, max fields per entity, payload-size and depth limits. Parameterized `[Theory]` tables per type. |
| Application unit | Definition CRUD handlers, deactivate/reactivate, option removal, the mass-assignment guard (unknown or inactive keys), and building the filter expression from an **allow-listed, parameterized** model. |
| Integration (Testcontainers SQL Server, mandatory) | `JSON_VALUE`/`OPENJSON` filtering and sorting, `ISJSON` check constraint, `CustomFieldIndex` side-table consistency (BA recommendation in `ba/core-domain.md` §8.7), org and entity global filters applied to JSON queries, SQL-injection attempts through keys, values and filter expressions, export CSV contents. **Not** InMemory or SQLite: their JSON functions differ from SQL Server's. |
| Contract | OpenAPI snapshot of `customFields` as a `{ [key]: value }` object on DTOs and of the `/custom-field-definitions` endpoints; a ProblemDetails `errors["customFields.<key>"]` shape. |
| Component (Jest) | The dynamic ng-zorro control renderer for each type, read-only display, list-column formatter, sanitized labels. |
| E2E (Playwright) | One journey: define a field → it appears on the form → save → list column/filter → deactivate → it is hidden but the value is preserved. |
| Performance (k6, nightly) | List filter on a searchable custom field with ≥ 100k rows per org: p95 < 500 ms (§13.2 list target) when indexed, plus a baseline without the index to show the difference. |

---

## 9. Flaky test policy

- A flaky test is one that fails and then passes on the same commit without a code change.
- xUnit: **no automatic retries**. Playwright: `retries: 2` in CI only; a test that passes on retry is reported as *flaky* in the HTML report and counted.
- Within 1 working day of detection: tag it `[Trait("Quarantine","true")]` or `test.fixme`, open a bug (template in `bug-report-template.md`, label `flaky`), and assign an owner.
- Quarantined tests still run nightly in a non-blocking job. Fix or delete within **5 working days**. More than 10 quarantined tests freezes feature merges until the count is back under 10.
- **Never quarantinable**: the `Isolation` and `Authorization` traits and the auth smoke. If they flake, the build stays red until they are fixed.
- Common causes to rule out first: shared seed data mutated by another test, real clock usage (use `IClock`), arbitrary sleeps (use Playwright auto-wait and `expect.poll`), port clashes, container start-up (use the Testcontainers wait strategy).

---

## 10. Defect management

- Template: `bug-report-template.md`. Severity: S1 Critical (data leak, data loss, ledger imbalance, authz bypass, outage), S2 Major (core flow broken, no workaround), S3 Minor (workaround exists), S4 Trivial (cosmetic).
- Priority P0–P3 is set at triage. S1 implies P0.
- Every S1 or S2 fix ships with a regression test at the lowest layer that reproduces it.

---

## 11. Entry and exit criteria per sprint

Entry: stories have acceptance criteria; API contract drafted; test cases written (by SQA) before or alongside development.
Exit: all P1 test cases pass; no open S1 or S2; nightly green for 2 consecutive runs; coverage gates met.

---

## 12. Definition of Done (per story)

- [ ] Acceptance criteria met and demoed against seeded data.
- [ ] Unit tests for domain and application logic; integration tests for every new or changed endpoint, including **a cross-org test, a cross-entity test (if entity-scoped), an allow test and a deny test for its permission**.
- [ ] Endpoint has `[HasPermission]` (or approved anonymous); entity implements `IOrgScoped`/`ILegalEntityScoped` as appropriate; no new `IgnoreQueryFilters` outside the allow-list.
- [ ] Industry-agnostic: core code has no module references and no industry vocabulary (architecture tests + vocabulary lint green); industry-specific code, tables, seed, menus and permissions live in the module.
- [ ] If the feature belongs to a module: API, route, menu and jobs respect the module gate.
- [ ] Angular: component tests; `data-testid` attributes on interactive elements (naming per `playwright/README.md`); route guard and `*hasPermission` applied.
- [ ] Audit events written for create, update and delete (§7.3) and verified in a test.
- [ ] Errors returned as ProblemDetails; validation errors mapped to fields (§10.2).
- [ ] No high or critical vulnerabilities; no secrets in code; sensitive fields masked as per §7.3.
- [ ] Migrations reversible or a documented forward-fix; migration applied in CI.
- [ ] All CI gates green; diff coverage ≥ 80%; reviewed by someone other than the author.
- [ ] Docs updated (API/OpenAPI, README, test cases file if behaviour changed).
- [ ] SQA has executed the manual and exploratory cases for the story and signed off on it.

---

## 13. Reporting

- Per PR: test summary, coverage diff, Playwright HTML report artifact.
- Nightly: dashboard of pass rate, flaky count, quarantine list and ZAP alerts.
- Per release: the release checklist (`release-checklist.md`) signed off by SQA and Muhammad.

---

## 14. Assumptions and decisions

Status: **DECIDED** = confirmed by the coordinator on 22 Oct 2026 (no longer an assumption); **ASSUMED** = SQA assumption still to confirm; **SUPERSEDED** = replaced by a decision.

| ID | Status | Assumption / decision | Source |
|---|---|---|---|
| A-T1 | ASSUMED | "Tenant" in this ERP means **organization** (`OrgId`); the UI calls it **workspace** (UX 00). In the spec, "tenant" also means a lessee. | §2.2, §9, UX 00 |
| A-T2 | SUPERSEDED by D-WS | ~~Org resolved only by sign-in key or subdomain.~~ Now: subdomain first; on the bare domain, membership lookup after the password check → auto-select (1 workspace) or picker (> 1). | §10.3, decision (b) |
| A-T3 | SUPERSEDED by D-WS | ~~Email unique per org.~~ Now: one global identity per email (password, lockout) with a membership per workspace; email unique within a workspace. The identity/membership model itself is an SQA assumption (A-T3'). | decision (b) |
| A-A1 | **DECIDED** | Lockout: **5 consecutive failures → 15 minutes**; the message is generic (no enumeration) and shows the retry time. Rate limit 5 attempts/min per IP+email stays an assumption. | decision 7 |
| A-A2 | ASSUMED | Refresh-token reuse revokes the whole token family and returns 401. | §7.3 |
| A-A3 | **DECIDED** (amended) | Logout revokes the refresh token, clears the cookie **and adds the access token's `jti` to a denylist** checked by every API instance; denylist entries expire with the token. **Logout-all** revokes all refresh tokens and all live `jti`s of the user. | decision 5 |
| A-A4 | ASSUMED | Admin reset, deactivation, role change and password change revoke the user's refresh tokens; role changes bump `ver`. | §4.26, §10.4 |
| A-A5 | ASSUMED | Refresh cookie `Path=/api/v1/auth`; antiforgery/custom header on cookie endpoints. | §7.3 |
| A-A6 | ASSUMED | Missing `X-Entity-Id` = all allowed entities; outside `ents` → 403; another org's record → 404. | §4.30, §10.3 |
| D-TOK | **DECIDED** | Access token TTL **5 minutes** (replaces the spec's 15). | decision 5 |
| D-KMSI | **DECIDED** | Ticked → 30-day persistent refresh token, **absolute** (rotation never extends it); unticked → session-only cookie that ends when the browser closes, with a **12 h absolute server maximum**. | decision 6 + final pass |
| D-WS | **DECIDED** | One email can belong to several workspaces: picker after login when > 1, auto-select when exactly 1, no picking a workspace you don't belong to, switching issues new org-scoped tokens **and revokes the old workspace's tokens**, no cross-workspace bleed. | decision (b) + last pass |
| D-PWR | **DECIDED** | Reset tokens are single-use, expire after 30 minutes; requesting a new link **invalidates all earlier ones** (confirmed); a successful reset invalidates all and **always signs out other devices** (refresh tokens revoked, jtis denylisted, no opt-out); generic response. | decision (c) + final pass |
| D-PWP | **DECIDED** | Password policy: 10–**128** characters with upper case, lower case and a digit; **any Unicode letter** counts toward upper/lower; must **not equal the email (case-insensitive)**, containing it is allowed; on registration, reset and change. | decision (d) + final pass + last pass |
| D-PERM | **DECIDED** | Permission keys are `<module>.<resource>.<action>` (catalogue in §14b). Unknown keys are rejected; module keys are inert while the module is disabled. | decision (a) |
| D-SENS | **DECIDED** | IDs and IBANs are masked without `core.sensitive.read`; default holders Org admin, Finance manager, Hr manager (accepted). | decision 4 + final pass |
| D-SAL | **DECIDED** | Salary is gated by `hr.payroll.read` (HR module), not `core.sensitive.read`; hidden on every core surface while HR is disabled (PERM-007d/e). Payroll ledger postings are aggregated per payroll run; per-employee detail is behind `hr.payroll.read` (PERM-007f). | final pass |
| D-CHQ | **DECIDED** | Cheques/PDCs are core finance (`finance.cheques.read/write`, core `/cheques`). **`finance.cheques.approve`** is the checker step for deposit, clear, bounce, replace and cancel (maker ≠ checker); **`finance.cheques.issue`** covers issuing an outgoing cheque, which **needs no approval** (DECIDED). Clearing creates a **core receipt**; `chequeDueAlertDays` is a **core finance setting**. RE only links cheques to leases/instalments; disabling RE keeps cheques and link data. | decision 3 + final pass |
| D-FIN | **DECIDED** | Sales invoices, purchase bills, receipts and payments are **core finance** (`finance.invoices/receipts/bills/payments.*`, core routes). `billing` and `payables` are **not** modules. | final pass |
| D-RE | **DECIDED** | RE naming: module key `realestate`, UI `/real-estate/`, API `/api/v1/m/realestate/`, DB schema `re`, permission prefix **`realestate.`** (SQA choice: matches the module key, like `hr.`/`sample.`, so the registry maps prefix → module with no alias table). RE adds lease links, lease billing runs and lease-expiry alerts. | final pass |
| D-TOKF | **DECIDED** | Denylist store unavailable → **fail closed** (AUTH-066). | final pass |
| D-TSE | **DECIDED** | Test-only seeding endpoint `/api/v1/test-support/*`, registered only when `ASPNETCORE_ENVIRONMENT=Testing`; 404 elsewhere (TS-001). The onboarding E2E uses it. | final pass |
| D-TPL | **DECIDED** | Real-estate roles and the RE chart of accounts come from an **industry template seed**, not the core seed; `LeaseExpiryAlertDays` is an RE module setting (`ChequeDueAlertDays` moved to core finance, D-CHQ). | decision 2 + final pass |
| A-M1 | **DECIDED** (amended) | Core always on: workspace setup, legal entities, parties, users/roles/permissions, auth, settings, approvals, audit, documents, notifications, ledger, **sales invoices, receipts, purchase bills, payments, cheques/PDCs**, reports framework. Optional module: `hr`. Industry module `realestate`. Test-only: `sample`, `sample-addon`. | decisions 1–3 + final pass |
| A-M2 | **DECIDED** | Module toggles need `core.modules.manage` (Org admin) within the plan; the plan is read-only to the workspace. | decision 1 |
| A-M3 | **DECIDED** | A disabled module keeps all data; its API returns **403 ProblemDetails `type: module-disabled`**; routes and menus are hidden; its jobs are skipped. | decision 1 |
| A-M4 | **DECIDED** | Module dependencies are enforced (409 when disabling a module others need). | decision 1 |
| A-M5 | **DECIDED** | Re-enabling restores the retained data with no migration; jobs resume from now. | decision 1 |
| A-M6 | **DECIDED** | Pending approvals of a disabled module are hidden and cannot be decided until it is re-enabled. | decision 1 |
| A-M7 | **DECIDED** | Module state cached per org, invalidated on toggle (≤ 60 s elsewhere). | decision 1 |
| A-M8 | **DECIDED** | Module toggles are audited. | decision 1 |
| A-M9 | **DECIDED** | Onboarding picks one industry, which pre-selects modules and applies its industry template; "Other / General business" = core only. | decision 1 + UX 03 |
| A-M10 | **DECIDED** | Module routes are `/api/v1/m/{key}/…`; the old flat paths return 404. | decisions 1–2 |
| A-CF1 | **DECIDED** (limits) | Field types per BA §8.3; limits **50 active fields per record type, 10 searchable** (DECIDED); SQA limits JSON ≤ 16 KB, depth ≤ 2 remain assumptions. | §9 + BA §8 + final pass |
| A-CF2 | ASSUMED | Deactivated field values are kept, hidden, excluded from export by default, writes rejected; admin read via `?includeInactiveFields=true`. | BA CF-LIF |
| A-CF3 | ASSUMED (BA-aligned) | Filterable fields indexed via the `CustomFieldIndex` side table; no runtime DDL. | BA §8.7 |
| A-CF4 | **DECIDED** (key format) | Managing definitions needs `core.customfields.manage` (was `customization.fields.manage`); reading `core.customfields.read`. | decision (a) + BA §8.2 |

### 14b. Permission key catalogue (DECIDED format `<module>.<resource>.<action>`)

Grants per role come from spec §7.2 through this mapping; the generated rows are in `permission-matrix-cases.csv` (450 rows).

| New key(s) | Was (spec §7.1 or earlier decision) | Module / route prefix | Grants derived from |
|---|---|---|---|
| `core.entities.read/write` | `portfolio` (legal entities), then `org.entities.*` | core `/entities` | Org admin RW, others R |
| `core.customers.read/write` | `leasing` (customers) | core `/customers` | spec leasing column |
| `core.vendors.read/write` | `payables` (vendors) | core `/vendors` | spec payables column |
| `finance.accounts.read/write`, `finance.journals.read/write` | `accounting` | core `/accounts`, `/journals` | spec accounting column |
| `finance.cheques.read/write` | `leasing / billing` (§12) | core `/cheques` | max(billing, leasing) |
| `finance.cheques.approve` | — (new) | core | holders of `core.approvals.decide`; checker for deposit/clear/bounce/replace/cancel |
| `finance.cheques.issue` | — (new) | core `/cheques/outgoing` | spec payables RW holders |
| `finance.invoices.*`, `finance.receipts.*` | `billing` | core `/invoices`, `/receipts` | spec billing column |
| `finance.bills.*`, `finance.payments.*` | `payables` | core `/bills`, `/payments` | spec payables column |
| `hr.employees.*`, `hr.payroll.*` | `hr` | `/m/hr/…` | spec hr column; `hr.payroll.read` also unmasks salary |
| `realestate.properties.*` | `portfolio` (properties, units) | `/m/realestate/…` | spec portfolio column |
| `realestate.acquisitions.*` | `investment` | `/m/realestate/…` | spec investment column |
| `realestate.leases.*` | `leasing` (leases) | `/m/realestate/…` | spec leasing column |
| `realestate.workorders.*` | `facilities` | `/m/realestate/…` | spec facilities column |
| `realestate.deals.*` | `brokerage` | `/m/realestate/…` | spec brokerage column |
| `core.reports.read`, `realestate.reports.read` | `reports` | core `/reports/{slug}`, `/m/realestate/reports/{slug}` | spec reports column |
| `core.approvals.decide` | `approvals.decide` | core | spec |
| `core.audit.read` | `admin.audit` | core | spec |
| `core.settings.read/write`, `core.modules.manage` | `admin.settings` | core | spec |
| `core.users.read/write`, `core.roles.write` | `admin.users` | core | spec |
| `core.customfields.read/manage` | `customization.fields.read/manage` (BA) | core | BA CF-SEC-01/02 |
| `core.sensitive.read` | `sensitive.read` (decision 4) | core | Org admin, Finance manager, Hr manager |
| `sample.items.read/write` | — (test-only module) | `/m/sample/…` | test fixtures only |

### 14c. Gaps raised after the decisions

| ID | Gap | Status / resolution |
|---|---|---|
| G-PERM-1 | `sensitive.read` vs 3-part format | **DECIDED**: `core.sensitive.read` |
| G-PERM-2 | Scope of `finance.cheques.approve` | **DECIDED**: checker for deposit, clear, bounce, replace, cancel; `finance.cheques.issue` for outgoing (CHQ-013…020) |
| G-PERM-3 | Default grants for new keys | **DECIDED**: proposed defaults accepted (§14b) |
| G-PERM-4 | Salary masking | **DECIDED**: `hr.payroll.read` (PERM-007d/e) |
| G-PERM-5 | Prefix `re` vs module key `realestate` | **DECIDED**: prefix `realestate.` (D-RE) |
| G-CHQ-1 | Cheque clearing needs receipts from the optional `billing` module | **DECIDED**: receipts are core (CHQ-011) |
| G-CHQ-2 | `chequeDueAlertDays` placement | **DECIDED**: core finance setting (CHQ-012, CORE-027) |
| G-KMSI-1 | Server lifetime of a session-only token | **DECIDED**: 12 h absolute (AUTH-073) |
| G-KMSI-2 | Sliding vs absolute 30 days | **DECIDED**: absolute (AUTH-025/072) |
| G-TOK-1 | Denylist outage | **DECIDED**: fail closed (AUTH-066) |
| G-PWR-1 | Older reset tokens on a new request | **DECIDED**: all invalidated (PWR-004) |
| G-PWP-1 | Unicode classes, max length, equal to email | **DECIDED**: any Unicode letter counts; max 128; must not equal the email, case-insensitive; containing the email is allowed (PWP-019/020/023–025) |
| G-CF-L | Custom-field limits (UX 100/5 vs BA 50/10) | **DECIDED**: 50 per record type, 10 searchable |
| G-SEED | Test provisioning hook | **DECIDED**: Testing-env-only endpoint (TS-001…003) |
| G-WS-1 | Switching workspace revokes the old workspace's tokens | **DECIDED**: revoked (WS-006); the in-app switcher entry point is still a UX item (G-UX-4) |
| G-PWR-2 | Opt-out of signing out other devices on reset | **DECIDED**: a successful reset always signs out other devices; no opt-out (PWR-009/009a/009b) |
| G-PAY-1 | Payroll postings in the core ledger | **DECIDED**: aggregated per payroll run; per-employee detail behind `hr.payroll.read` (PERM-007f) |
| G-CHQ-3 | Does issuing an outgoing cheque need approval? | **DECIDED**: no; approve only for deposit, clear, bounce, replace, cancel (CHQ-017/018) |
| G-UX-9 | UX puts the PDC register under Real estate; cheques are core | **Open**; UX nav to update |
| G-UX-* | Mismatches between the UX docs and the decisions | See the SQA report / `sprint0-1-test-cases.md` §8 |
