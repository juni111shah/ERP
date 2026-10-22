# Sprint 0–1 Test Cases: Tenancy, Permissions, Modules, Auth, Industry-agnostic Core, Custom Fields

Owner: SQA · v0.1 · 22 Oct 2026 (PKT, UTC+5) · Spec: `/workspace/erp-team/spec.txt` (§ = spec section). Assumption IDs (A-…) are defined in `test-strategy.md` §14.

**Legend**
- **Priority**: P1 = must pass to merge or release; P2 = must pass before the release; P3 = nice to have.
- **Type**: Func (functional), Neg (negative), Sec (security), Perf, Arch (architecture), Contract, A11y.
- **Automation**: Unit (xUnit), App (Application-layer xUnit), API (WebApplicationFactory + Testcontainers SQL Server), Comp (Angular Jest + Testing Library), E2E (Playwright), Arch (NetArchTest/lint), Manual.
- **Org A** = "Harbour & Oasis Group (Demo)" (`demo`), entities ENT-0001 Harbour Assets LLC and ENT-0002 Oasis Investments LLC. **Org B** = "Isolation Test Org" (`iso-b`) with mirrored codes (see strategy §4).
- "Tenant" in this document means **organization** (`OrgId`), never the lessee (A-T1).
- All API paths are relative to `/api/v1` (§12). Responses are ProblemDetails on error.
- **UI routes** follow UX `uiux/08-navigation-ia.md` (`/auth/login`, `/auth/forgot-password`, `/auth/reset-password`, `/onboarding/*`, `/finance/journals`, `/sales/invoices`, `/admin/*`, `/real-estate/*`).
- **Routing (DECIDED, final):** core endpoints stay flat: `/customers`, `/vendors`, `/entities`, `/cheques`, `/invoices`, `/receipts`, `/bills`, `/payments`, `/journals`, `/accounts`, `/bank-accounts`, `/reports/{core-slug}` (incl. receivables and payables ageing), `/approvals`, `/users`, `/settings`, `/audit`, `/search`. **Sales invoices, purchase bills, receipts and payments are core finance** (billing and payables are not modules). Module endpoints are `/m/{key}/…`: `realestate` (properties, units, acquisitions, leases, lease billing runs, work-orders, deals, RE reports; UI `/real-estate/*`, DB schema `re`), `hr` (employees, attendance, payroll-runs, gratuity), test-only `sample`. The spec's flat module paths (§12) return 404 (CORE-030).
- **Change log v0.2 (22 Oct 2026, coordinator decisions):** core permissions for legal entities (`core.entities.*`); RE settings, roles and CoA move to the RE module or template; cheques/PDCs are core finance (`finance.cheques.*`); masking is gated by `core.sensitive.read` (decided as `core.sensitive.read`, mapped to the key format); access token TTL 5 min + jti denylist on logout; "Keep me signed in" (30-day persistent vs session-only); lockout 5 failures / 15 min with a generic message that shows the retry time. **v0.3:** permission keys are `<module>.<resource>.<action>`; one email can belong to several workspaces (picker); password reset tokens are single-use, 30 min; password policy ≥ 10 chars with upper, lower and a digit.
- **v0.4 (final pass):** billing/payables are core finance; RE permission prefix `realestate.`; `finance.cheques.approve`/`issue`; salary gated by `hr.payroll.read`; [proposed] grants accepted; session-only 12 h max; denylist fails closed; Unicode letters count, max 128; test-only seeding endpoint (Testing env only).
- **Permission keys (DECIDED v0.3):** `<module>.<resource>.<action>`. The full catalogue and the old→new mapping are in `test-strategy.md` §14b. Examples: `core.users.write` (was `admin.users`), `core.approvals.decide` (was `approvals.decide`), `finance.journals.write` (was `accounting.write`), `finance.invoices.write`, `realestate.leases.write` (was `leasing.write`), `finance.cheques.approve`, `core.sensitive.read`, `core.customfields.manage`.

---

## 1. Tenant (organization) isolation

Spec basis: §7.3 "every table has OrgId; an EF Core global query filter uses the org from the token; unique indexes include OrgId"; §10.3 "Org resolved from the token (claim org), never from the client body".

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| TI-001 | List endpoints return only own-org rows | Org A and Org B seeded with customers, properties, vendors, journals | As an Org A org admin, call GET on every list endpoint in §12 (`/customers`, `/m/realestate/properties`, `/m/realestate/units`, `/m/realestate/leases`, `/cheques`, `/invoices`, `/receipts`, `/vendors`, `/bills`, `/payments`, `/m/realestate/work-orders`, `/accounts`, `/bank-accounts`, `/journals`, `/m/hr/employees`, `/m/realestate/deals`, `/approvals`, `/audit`, `/users`, `/notifications`) | Every item has OrgId = A (asserted in the DB by Id); 0 Org B ids are present; the counts equal the Org A seed counts | P1 | Sec | API (theory over the endpoint list) |
| TI-002 | GET by id of another org's record → 404 | Org B customer CUS-0001 has id `B1` | As Org A, call GET `/customers/B1` (repeat for every `/{id}` route) | 404 ProblemDetails, the same as for a random GUID (no 403, no existence disclosure, A-A6); no Org B data in the body | P1 | Sec | API (theory) |
| TI-003 | PUT/DELETE/action on another org's id → 404, no change | Org B records exist | As Org A: PUT `/customers/B1`, DELETE `/m/realestate/properties/B…`, POST `/m/realestate/leases/B…/activate`, POST `/invoices/B…/void`, POST `/approvals/B…/approve` | 404; the Org B row is unchanged (RowVersion is the same); no AuditEvent is written in either org | P1 | Sec | API |
| TI-004 | OrgId in the request body is ignored | Org A token | POST `/customers` with body `{"orgId":"<OrgB>", ...}` | 201; the row is stored with OrgId = A; the response has no orgId field (or it equals A) | P1 | Sec | API |
| TI-005 | Foreign-key references to another org's records are rejected | Org B unit id `BU1`, Org B customer `BC1` | As Org A, POST `/m/realestate/leases` with `unitId=BU1`, `customerId=BC1` | 400/404 validation error ("unit not found"); no lease is created; the Org B unit status is unchanged | P1 | Sec | API |
| TI-006 | Same business codes in two orgs do not collide | Both orgs have CUS-0001, PROP-0001, ENT-0001 | Create CUS-0013 in A and then in B; search `q=CUS-0001` in A | Both creates succeed (unique index includes OrgId); the search returns only the Org A record | P1 | Func | API |
| TI-007 | Number sequences are per org | — | Create an invoice in A and in B on the same day | Each org's sequence advances independently (INV-2026-…); A's number does not consume B's sequence | P2 | Func | API |
| TI-008 | The org claim cannot be changed by the client | Valid Org A token | Re-sign the token with `org=B` using a wrong key; also send header `X-Org-Id: B`, query `?orgId=B`, cookie `org=B` | Tampered token → 401; the headers, query and cookie are ignored and the Org A data is returned | P1 | Sec | API |
| TI-009 | The EF global filter is registered on every org-scoped entity | — | A reflection test over `DbContext.Model.GetEntityTypes()`: each `IOrgScoped` type has a query filter that references `OrgId` | Test fails if any entity lacks the filter (CI gate 5) | P1 | Arch | Unit |
| TI-010 | `IgnoreQueryFilters()` only appears in allow-listed code | — | A Roslyn analyzer or source scan for `IgnoreQueryFilters(`; compare with `allowlist.json` (for example the platform admin and the migration seeder) | Fails on any new use; each allow-listed use has a comment and a test showing it re-applies an explicit OrgId predicate | P1 | Arch | Unit |
| TI-011 | Allow-listed `IgnoreQueryFilters` still enforces the org | The soft-delete "restore" query uses `IgnoreQueryFilters` to see `IsDeleted` rows | As Org A, restore the soft-deleted Org B vendor id | 404; Org A can restore only its own deleted rows | P1 | Sec | API |
| TI-012 | Raw SQL and Dapper reporting queries are org-scoped | Reports use views or Dapper (§10.7) | Call GET `/reports/{slug}` for each of the 16 slugs as Org A; compare against an independent SQL sum restricted to Org A | Totals equal the Org A sums; a seeded Org B "canary" amount (e.g. 999,999.99) never appears | P1 | Sec | API |
| TI-013 | CSV/PDF exports are org-scoped | — | GET `/customers/export.csv`, `/m/realestate/reports/rent-roll/export.csv`, `/reports/trial-balance/export.pdf`, `/invoices/{A-id}.pdf` | The files contain only Org A rows; the Org B canary names are absent; an Org B id in the PDF route → 404 | P1 | Sec | API |
| TI-014 | Global search is org-scoped | Org B customer "Okafor-B Canary" | As Org A, GET `/search?q=Okafor` | Only "Mr. Daniel Okafor, CUS-0012" (Org A) is returned (§4.28) | P1 | Sec | API |
| TI-015 | Audit log is org-scoped | Actions performed in both orgs | As the Org A admin, GET `/audit` and `/audit/{OrgB-event-id}` | Only Org A events are listed; the B event → 404 | P1 | Sec | API |
| TI-016 | Cache keys include OrgId | Redis/IMemoryCache enabled (§10.7: reference data, dashboard 60 s, reports by filters) | Warm `/dashboard`, `/accounts` and `/reports/trial-balance?asOf=…` as Org A; then call the same URLs with the same filters as Org B | Org B gets its own values (not A's cached payload); inspect the cache: every key is prefixed with the org (and entity scope) | P1 | Sec | API |
| TI-017 | Cache invalidation is org-local | — | Post a journal in A (invalidates A's report cache); immediately read B's report | B's cache entry is untouched (hit) and still correct; A's is recomputed | P2 | Func | API |
| TI-018 | Background jobs run per org with an org context | Hangfire jobs (§10.6): billing run, lease status, alerts builder, overdue flags | Trigger each job for Org A only (in-process) | Only Org A rows change; Org B leases, invoices and notifications are unchanged; the job log shows OrgId = A | P1 | Sec | API |
| TI-019 | Job without an org context fails closed | — | Enqueue a job with a null or empty org context | The job throws or no-ops; no rows of any org are read or written (the filter does not evaluate to "all orgs") | P1 | Sec | App |
| TI-020 | Billing run cannot target another org's lease | — | POST `/m/realestate/billing-runs {upTo}` as Org A while Org B has due instalments | Only Org A instalments are invoiced; counts match | P1 | Sec | API |
| TI-021 | SignalR notifications are org-scoped | Hub `/hubs/notifications` with groups per user and org (§10.7) | Connect an A user and a B user; create an approval in A | Only A users in A's group receive `approvalCreated`; the B client receives nothing; joining group `org:B` from an A connection is refused | P1 | Sec | API |
| TI-022 | Document download is org-scoped | Documents with SAS URLs valid 5 minutes (§10.7) | As Org A, GET `/documents/{OrgB-docId}/download`; also replay an Org B SAS URL after 5 minutes | 404; an expired SAS URL → 403 from storage | P1 | Sec | API |
| TI-023 | Notification read endpoint is org-scoped | — | POST `/notifications/{OrgB-id}/read` as A | 404; the B notification stays unread | P2 | Sec | API |
| TI-024 | Concurrency/RowVersion does not reveal another org's record | — | PUT with `If-Match` on an Org B id | 404 (not 412) | P2 | Sec | API |
| TI-025 | SQL trigger backstop respects OrgId | Period-lock trigger (§7.3) | Lock Org A ENT-0001 to 30 Sep 2026; post an Org B journal dated 15 Sep 2026 | The Org B post succeeds (B is not locked); the Org A post on that date is rejected | P2 | Func | API |

### 1b. Legal-entity scoping within an org

Spec: §2.2 "Legal entity access: All legal entities or Only selected entities"; §4.30 "options limited to user's entity access… send as X-Entity-Id"; §7.3 "second global filter on LegalEntityId… the switcher only narrows within the allowed set".

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| ES-001 | Restricted user sees only allowed entities | `qa.leasing.oasis` limited to ENT-0002 | GET `/m/realestate/leases`, `/m/realestate/properties`, `/invoices`, `/bank-accounts`, `/dashboard` | Only Oasis rows (3 active leases, portfolio AED 81.8M, occupancy 30.0% per §2.2) | P1 | Sec | API |
| ES-002 | `X-Entity-Id` outside the allowed set → 403 | Same user | Send `X-Entity-Id: <ENT-0001 id>` | 403 ProblemDetails (A-A6); no data | P1 | Sec | API |
| ES-003 | `X-Entity-Id` of another org's entity → 403/404 | Org A user | `X-Entity-Id: <Org B ENT-0001 id>` | 403 or 404, never Org B data | P1 | Sec | API |
| ES-004 | Header narrows within the allowed set | Admin (all entities) | `X-Entity-Id: ENT-0002` vs no header | Narrowed results equal the Oasis subset; no header = both entities | P1 | Func | API |
| ES-005 | Create in a non-allowed entity is rejected | Restricted user | POST `/invoices` with `legalEntityId=ENT-0001` | 403; no row is created | P1 | Sec | API |
| ES-006 | Get by id of a non-allowed entity's record | Restricted user | GET `/m/realestate/leases/{Harbour lease id}` | 404 | P1 | Sec | API |
| ES-007 | Shared, non-entity-scoped data is visible to both | §2.2: CoA, customers, vendors, users and roles are shared across entities | Restricted user GET `/customers`, `/accounts` | All org customers and accounts are visible (by design); invoices of those customers are still filtered by entity | P2 | Func | API |
| ES-008 | Entity switcher options = allowed entities | — | Log in as the restricted user; open the switcher | Only "Oasis Investments LLC" (no "All legal entities" or Harbour) | P1 | Func | E2E |
| ES-009 | Removing entity access takes effect on the next token | Admin removes ENT-0002 from a user | User calls GET with the old access token after the `ver` bump (A-A4) | 401 → refresh → the new token's `ents` lacks ENT-0002; data is hidden | P1 | Sec | API |
| ES-010 | Reports and exports respect the entity scope | Restricted user | GET `/m/realestate/reports/rent-roll`, `/reports/receivables-ageing/export.csv` | Only ENT-0002 rows; total AR 29,062.50 (§14.1) | P1 | Sec | API |

---

## 2. Permission policies

Spec basis: §7.1 keys, §7.2 matrix, §7.3 `[HasPermission]` + policy provider, §10.4 the MediatR authorization behaviour (defence in depth) and `ver`.

### 2a. Role × permission matrix (data-driven)

The full matrix (9 roles × 17 resources × read/write + 16 single-action rows = **450 cases**, regenerated in v0.4 with the `<module>.<resource>.<action>` keys) is in **`/workspace/erp-team/sqa/permission-matrix-cases.csv`**. Grants derive from spec §7.2 via the mapping in strategy §14b (all defaults DECIDED). The RE roles exist only after the RE industry template has been applied (CORE-029). It is generated from §7.2 and run as one xUnit `[Theory]` with `[ClassData]`. Each case logs in as the seeded `qa.<role>` user and calls a representative endpoint per area. Expected 200/201 when granted; **403 when denied, with an assertion that no row and no audit event was created**.

Representative endpoints: one GET (read) and one POST (write) per resource, for example `core.entities` → `/entities`, `finance.cheques` → `/cheques`, `realestate.leases` → `/m/realestate/leases`, `finance.invoices` → `/invoices`; `core.sensitive.read` → `GET /customers/{id}` (masked vs unmasked); `core.approvals.decide` → `POST /approvals/{id}/approve`.

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| PERM-001 | Matrix theory: all 450 rows | QA users seeded per role; all modules enabled | Run `PermissionMatrixTests` | Every row matches the CSV | P1 | Sec | API |
| PERM-002 | Org admin has every key | RE template applied | Decode the Org admin access token `perms` | It contains every registered key of the core and enabled modules (strategy §14b catalogue) | P1 | Func | API |
| PERM-003 | Viewer is read-only across all areas | `qa.viewer` | POST/PUT/DELETE against one endpoint per area | 403 every time; GET 200 | P1 | Sec | API |
| PERM-004 | Approver: read everything + decide only | `qa.approver` | Approve a pending bill created by another user; try POST `/bills` | Approve 200; create 403 | P1 | Func | API |
| PERM-005 | Accountant cannot approve | `qa.accountant`; a pending approval by another user | POST `/approvals/{id}/approve` | 403 ("Cannot approve", §2.3) | P1 | Sec | API |
| PERM-006 | Hr manager sees HR; Leasing manager does not | — | `qa.leasing` GET `/m/hr/employees`; `qa.hr` GET `/m/hr/employees` | 403; 200 | P1 | Sec | API |
| PERM-007 | IDs and IBANs are masked without `core.sensitive.read` (DECIDED) | Holders: Org admin, Finance manager, Hr manager | `qa.leasing` and `qa.accountant` (no `core.sensitive.read`) GET `/customers/{id}` (Emirates ID, passport), `/vendors/{id}` (IBAN), `/bank-accounts/{id}` (IBAN); `qa.finance` and `qa.hr` the same; also CSV export, global search results and audit before/after JSON | Without the permission: masked on every surface (e.g. `784-****-*******-1`, `AE** **** **** **** ***1 234`); with it: full values. `core.sensitive.read` does **not** reveal salary (PERM-007d) | P1 | Sec | API |
| PERM-007a | Masking cannot be bypassed by filter or sort | `qa.leasing` (no `core.sensitive.read`) | `GET /customers?q=784-1985` (searching a full Emirates ID); `sort=idNumber`; `?fields=idNumber` | Search on sensitive fields is not allowed without the permission (no match or 400; never an oracle that confirms a full ID); sort is disabled for masked columns | P1 | Sec | API |
| PERM-007b | Writing a masked field does not wipe it | `qa.leasing` edits a customer and sends the masked value back unchanged | PUT `/customers/{id}` with `idNumber="784-****-*******-1"` | The stored value is unchanged (a masked placeholder is never persisted); changing the field without `core.sensitive.read` → 403 **[ASSUMPTION]** | P1 | Sec | API |
| PERM-007c | `core.sensitive.read` is grantable to custom roles and audited | Org admin | Grant it to a custom role; read the audit log | The grant is audited; `/me` perms include it; the next token refresh unmasks | P2 | Func | API |
| PERM-007d | Salary is gated by `hr.payroll.read`, not `core.sensitive.read` (DECIDED) | Custom role A: `hr.employees.read` + `core.sensitive.read`, no `hr.payroll.read`; custom role B: `hr.employees.read` + `hr.payroll.read` | GET `/m/hr/employees/{id}` as A and as B; CSV export; audit diff of an Employee update | A: Emirates ID/IBAN visible, salary fields (basic, allowances, deductions, net) hidden on every surface; B: salary visible, IDs/IBANs masked | P1 | Sec | API |
| PERM-007e | Salary stays hidden while HR is disabled | HR module disabled; Org admin (holds `hr.payroll.read`) | GET `/m/hr/employees/{id}`; GET `/audit?recordType=Employee`; `/search`; core exports | HR routes → 403 `module-disabled`; audit before/after JSON, search and exports show no salary values (an inert `hr.payroll.read` does not unmask) | P1 | Sec | API |
| PERM-007f | Payroll postings in the core ledger | HR enabled, a posted payroll run | `qa.accountant` (`finance.journals.read`, no `hr.payroll.read`) opens the payroll journal | Aggregated lines are visible (the ledger is core); postings are aggregated per payroll run (DECIDED); no per-employee salary lines or `EmployeeId` drill-down; per-employee detail only via `/m/hr/payroll-runs/{id}` with `hr.payroll.read` | P2 | Sec | API |
| PERM-008 | Multi-role = union of permissions | Accountant + Approver | GET `perms`; approve another user's request; create a journal | Union of both roles; both actions allowed | P2 | Func | API |
| PERM-009 | Maker-checker: cannot approve your own request, regardless of role | Org admin creates a General approval | The same Org admin calls POST `/approvals/{id}/approve` | 403/409 "Approvers can never approve their own requests" (§5.8, §7.3); status stays Pending | P1 | Sec | API |
| PERM-010 | Maker-checker on documents | `qa.finance` submits a bill > AED 10,000 | `qa.finance` approves their own bill's request | Rejected; another approver can approve | P1 | Sec | API |
| PERM-011 | Escalation: non-admin cannot edit roles or users | `qa.finance` | PUT `/users/{self}` with `roles:["Org admin"]`; POST `/users` | 403; roles are unchanged | P1 | Sec | API |
| PERM-012 | Escalation: the admin endpoint ignores extra fields | Org admin edits a Viewer | PUT `/users/{id}` with `{"perms":["core.users.write"], "isBuiltIn":true, "orgId":"B"}` | Only name, roles, entity access and active change (mass-assignment guard); the extra fields are ignored or 400 | P1 | Sec | API |
| PERM-013 | Escalation: the perms claim cannot be forged | `qa.viewer` token | Edit the JWT payload to add `finance.invoices.write`; keep the signature | 401 (signature invalid) | P1 | Sec | API |
| PERM-014 | Role change forces a re-evaluation (`ver`) | `qa.accountant` logged in | Admin removes the Accountant role; the user calls POST `/journals` with the old access token | 401 (`ver` mismatch) → refresh issues a token without `finance.journals.write` → 403 | P1 | Sec | API |
| PERM-015 | Deactivated user is blocked immediately | — | Admin unticks "Active (can sign in)"; the user calls an API with a live access token, then refreshes | API 401 (`ver`/active check); refresh 401; login rejected (§4.26) | P1 | Sec | API |
| PERM-016 | Defence in depth: MediatR behaviour blocks a missing attribute | Test-only controller action without `[HasPermission]` that sends `CreateInvoiceCommand` | Call it as `qa.viewer` | 403 from the authorization behaviour (§10.4) | P1 | Sec | App |
| PERM-017 | Endpoint inventory: every action is protected | — | Reflection over all controllers or minimal-API endpoints | Each has `[HasPermission]`, an allow-listed `[AllowAnonymous]`, or an explicitly documented "any authenticated" (§12: search, notifications, `/me`, general approvals) | P1 | Arch | Unit |
| PERM-018 | IDOR on "any"-permission endpoints | `qa.viewer` | POST `/approvals/{otherUsersRequest}/withdraw`; GET `/notifications` (another user's id); POST `/notifications/{otherUsersId}/read` | Withdraw only by the requester → 403; notifications only your own → 404 | P1 | Sec | API |
| PERM-019 | IDOR on documents: owner permission | `qa.hr` (no `finance.invoices.read`) | GET `/documents?ownerType=Invoice&ownerId=…`; download an invoice attachment | 403, because documents inherit the owner's permission (§12) | P1 | Sec | API |
| PERM-020 | System posting does not require the caller's `finance.journals.write` | `qa.leasing` (`finance.invoices.write`, no `finance.journals.*`) issues an invoice | POST `/invoices/{id}/issue` | 200; the journal Dr 1100 / Cr 4000 / Cr 2100 is posted by the posting service under system context (§5.1 step 6); the user still cannot POST `/journals` (403) | P1 | Func | API |
| PERM-021 | Cheque permissions (DECIDED) | `qa.facilities` (`finance.cheques.read`, `finance.cheques.issue`); `qa.leasing` (`finance.cheques.read/write`) | `qa.facilities` POST `/cheques/{id}/deposit`; `qa.leasing` POST `/cheques` and `/cheques/{id}/deposit` | 403; 201 and 202 (the deposit becomes a pending request awaiting `finance.cheques.approve`, CHQ-013) | P1 | Sec | API |
| PERM-021a | Legal entities use `core.entities.*` (DECIDED; the coordinator's `org.*` mapped to the key format) | `qa.leasing` (`realestate.properties` RW, `core.entities.read`); Org admin | `qa.leasing` POST `/entities`, PUT `/entities/{id}`; Org admin the same; `realestate` disabled → Org admin GET/POST `/entities` | 403; 200; works with RE disabled. `realestate.properties.*` grants nothing on `/entities` | P1 | Sec | API |
| PERM-030 | Every registered key matches the format (DECIDED) | All modules installed | Registry test: enumerate `IPermissionRegistry` keys | Every key matches `^[a-z][a-z0-9]*\.[a-z][a-z0-9]*\.[a-z][a-z0-9]*$` (exactly 3 lower-case segments); module keys start with the module's permission prefix; no duplicates | P1 | Arch | Unit |
| PERM-031 | Unknown key is rejected | Org admin | POST/PUT `/roles` with perms `["core.users.fly"]`, `["realestate.lease.read"]` (typo), `["admin.users"]` (legacy), `["billing.write"]` (legacy 2-part), `["Core.Users.Write"]` (case), `["core.users.write "]` (whitespace) | 400 ProblemDetails listing each unknown key; the role is unchanged; nothing is silently dropped | P1 | Neg | API |
| PERM-032 | Code that references an unknown key fails at start-up | Test build with `[HasPermission("core.user.write")]` (typo) | Start the host | Start-up validation fails (or a unit test over all attributes fails) | P1 | Arch | Unit |
| PERM-033 | A module key is inert while its module is disabled (DECIDED) | Custom role holds `realestate.leases.write` and `core.users.read`; `realestate` disabled | POST `/m/realestate/leases`; GET `/users`; GET `/me` | Lease → 403 `module-disabled`; users → 200; `/me` lists `realestate.*` keys as inactive (or omits them) **[ASSUMPTION: flagged inactive]**; Angular hides RE actions | P1 | Sec | API |
| PERM-034 | A module key becomes effective again on re-enable without a role edit | After PERM-033 | Enable `realestate`; refresh the token | `realestate.leases.write` works; the role was never edited | P1 | Func | API |
| PERM-035 | Keys of a module that is not installed cannot be assigned | `sample` not installed | PUT a role with `sample.items.read` | 400 unknown key (installed-but-disabled module keys **can** be assigned and stay inert) | P2 | Neg | API |
| PERM-036 | A module cannot register keys outside its prefix | Test module that registers `core.users.write` or `finance.cheques.approve` | Start the host | Registration rejected at start-up | P1 | Sec | Unit |
| PERM-037 | Token carries only valid keys | — | Decode tokens of every QA user | `perms` contains only registered 3-part keys; no legacy area keys | P2 | Contract | API |
| PERM-022 | Angular hides what the API refuses | `qa.viewer` | Open `/sales/invoices` | No "New invoice", Issue or Void buttons (`*hasPermission`); a direct nav to `/sales/invoices/new` is redirected by the route guard to a 403 page | P1 | Func | E2E + Comp |
| PERM-023 | Hidden button ≠ security | `qa.viewer` | Call the API behind the hidden button directly | 403 (PERM-003) | P1 | Sec | API |
| PERM-024 | Bell badge only for `core.approvals.decide` | §4.3 | `qa.accountant` vs `qa.approver` GET the badge count | Accountant 0 or hidden; Approver = pending approvals by others | P2 | Func | API + E2E |
| PERM-025 | `/me` reflects permissions and entities | — | GET `/me` per QA user | `perms` and `ents` equal the seed; no other org's info | P1 | Func | API |
| PERM-026 | Reset password requires `core.users.write` | — | `qa.finance` POST `/users/{id}/reset-password` | 403 | P1 | Sec | API |
| PERM-027 | Org admin cannot lock themselves out | Single Org admin | Remove your own Org admin role or deactivate yourself | 409 "the organization must keep at least one active Org admin" **[ASSUMPTION]** | P2 | Neg | API |
| PERM-028 | Period lock/reopen needs `core.entities.write` (Org admin) and a reason | §5.9 | `qa.finance` POST `/entities/{id}/reopen`; Org admin without a reason; Org admin with a reason | 403; 400; 200 + AuditEvent | P2 | Sec | API |

---

## 3. Module enable/disable flow

The spec does **not** define module toggling. Assumptions A-M1…A-M10 were **DECIDED** by the coordinator on 22 Oct 2026: core always on; disabled module → 403 `module-disabled`, data kept; module routes `/api/v1/m/{key}/…`. Module keys (final): core (always on: legal entities, parties, sales invoices, receipts, purchase bills, payments, cheques/PDCs, ledger); optional `hr`; industry `realestate` (properties, units, acquisitions, leases + cheque links, lease billing runs, work orders, deals, lease-expiry alerts); test-only `sample` and `sample-addon` (requires `sample`).

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| MOD-001 | List modules with their state | Org admin | GET `/modules` **[ASSUMPTION: endpoint name]** | Each module has key, name, isCore, isEnabled, dependencies and plan entitlement; core modules have `isCore=true` | P1 | Func | API |
| MOD-002 | Enable a module | `sample` disabled | POST `/modules/sample/enable` | 200; GET `/modules` shows it enabled; `/m/sample/items` returns 200 for a role holding `sample.items.read`; menu item and route appear after a refresh of `/me`; AuditEvent written (A-M8) | P1 | Func | API + E2E |
| MOD-003 | Disable a module | `realestate` enabled with data | POST `/modules/realestate/disable` | 200; its endpoints return 403 `type=module-disabled` (A-M3); its menu items are hidden; direct nav to `/real-estate/leases` shows the "This feature isn't enabled for your workspace" result (UX 08 §2) | P1 | Func | API + E2E |
| MOD-004 | Data is retained on disable | Count rows in the module's tables | Disable, then query the DB directly | Row counts and checksums are unchanged; nothing is deleted or soft-deleted | P1 | Func | API |
| MOD-005 | Re-enable restores access | After MOD-003/004 | Enable again | All prior records are accessible and identical (checksum); no duplicate seed rows | P1 | Func | API |
| MOD-006 | Only `core.modules.manage` can toggle | `qa.finance` | POST `/modules/sample/enable` | 403 | P1 | Sec | API |
| MOD-007 | Core modules cannot be disabled | — | POST `/modules/core/disable`, `/modules/accounting/disable` | 400/409 "core module" | P1 | Neg | API |
| MOD-008 | Dependency guard | Test-only `sample-addon` requires `sample` (A-M4) | Disable `sample` while `sample-addon` is enabled | 409 listing the dependents; nothing changes | P2 | Neg | API |
| MOD-009 | Plan entitlement guard | Org plan excludes `hr` | Enable `hr` | 403/409 "not in plan"; the plan is read-only to the org (§4.25) | P2 | Neg | API |
| MOD-010 | Permissions of a disabled module are inert | `qa.leasing` has `realestate.leases.write`; `realestate` disabled | POST `/m/realestate/leases` | 403 module-disabled (a role grant does not override the module gate); `/me` omits or flags those perms | P1 | Sec | API |
| MOD-011 | Role editor on a disabled module | Custom roles (§9) | Org admin opens the Roles editor | Disabled module keys are shown as unavailable or not assignable **[ASSUMPTION]** | P3 | Func | E2E |
| MOD-012 | Jobs of a disabled module are skipped | Lease status job (§10.6) | Disable `realestate`; run the nightly job | No lease status changes; the job logs "skipped: module disabled"; other modules' jobs run | P1 | Func | API |
| MOD-013 | Core records that reference a disabled module degrade gracefully | Invoices created by a lease billing run; `realestate` disabled | GET `/invoices/{id}`; open it in the UI | Invoice fully usable (core); the lease reference shows as plain text without a link; no 500 | P2 | Func | API + E2E |
| MOD-014 | Reports of a disabled module | `realestate` disabled | GET `/m/realestate/reports/rent-roll`, `/m/realestate/reports/occupancy` | 403 module-disabled; core reports (trial balance) still work and **still include historic journals posted by the module** (the ledger is core) | P1 | Func | API |
| MOD-015 | Global search excludes disabled modules | — | `/search?q=Marina` with `realestate` disabled | No property, unit or lease groups returned | P2 | Func | API |
| MOD-016 | Pending approvals of a disabled module | A pending payroll-run approval; `hr` disabled | GET `/approvals`; approve it | Hidden or flagged; decide → 409 until re-enabled (A-M6, DECIDED) | P2 | Func | API |
| MOD-017 | Cache/propagation of the module state | 2 API nodes or simulated | Disable on node 1; call on node 2 | Blocked within ≤ 60 s (A-M7); immediately on node 1 | P2 | Func | API |
| MOD-018 | Open UI session when a module is disabled | User has `/real-estate/leases` open | Admin disables `realestate`; the user clicks Save | API 403 module-disabled → friendly toast and redirect; no unhandled error | P2 | Func | E2E |
| MOD-019 | Toggle is idempotent | — | Enable twice; disable twice | 200 or no-op each time; one audit event per real change | P3 | Func | API |
| MOD-020 | Toggle is per org | (see also CORE-0xx) | Disable `realestate` in A | B is unaffected | P1 | Sec | API |

---

## 4. Login, refresh and logout

Spec basis: §4.1 (Email + Password, show/hide, inline alert, redirect to `/`, rate limit message), §7.3 (Identity PBKDF2/Argon2; JWT in memory; rotating refresh token in an HttpOnly Secure SameSite=Strict cookie; refresh on app start; lockout; antiforgery on cookie endpoints). **DECIDED overrides (22 Oct 2026):** access token TTL **5 min** (not 15) with a `jti` denylist checked by the API; refresh token **30 days persistent** when "Keep me signed in" is ticked, **session-only cookie** when unticked (replaces 14 days); lockout **5 failures → 15 min**, generic message that shows the retry time, §10.4 (claims `sub, org, perms, ents, ver`; RefreshToken table hashed, expiry, device, revoked; endpoints), §8 #1 (hard reload), #14 (show the retry time).

### 4a. Login

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| AUTH-001 | Login happy path (UI) | `admin@demo.the reference demo.local` active | Open `/auth/login`, enter valid credentials, click "Sign in" | Redirect to `/` (Overview); the avatar shows "Demo Administrator"; no token in localStorage or sessionStorage | P1 | Func | E2E (smoke) |
| AUTH-002 | Login happy path (API) | — | POST `/auth/login {email,password,rememberMe:false}` | 200 `{accessToken, expiresIn: 300}`; `Set-Cookie` refresh with `HttpOnly; Secure; SameSite=Strict`, Path `/api/v1/auth` (A-A5), **no Expires/Max-Age** (session cookie, AUTH-019); the JWT has `sub, org, perms, ents, ver, jti`, `exp` = iat + 5 min | P1 | Func | API |
| AUTH-003 | Refresh token is not in the response body | — | Inspect the login response JSON | No refresh token in the body | P1 | Sec | API |
| AUTH-004 | Wrong password | — | Valid email + wrong password | 401; generic message (no "wrong password" vs "no such user" distinction); the inline red alert shows it (§4.1) | P1 | Neg | API + E2E |
| AUTH-005 | Unknown email: no user enumeration | — | Compare the response for an unknown email vs a known email with a wrong password | The same status, body and similar timing (±50 ms median over 20 tries) | P1 | Sec | API |
| AUTH-006 | Required and format validation | — | Empty email; invalid email; empty password | Client: inline field errors, submit blocked; server: 400 ProblemDetails field errors | P2 | Neg | Comp + API |
| AUTH-007 | Show/hide password toggle | — | Type a password; click the eye icon twice | Type toggles password → text → password; `aria-pressed`/label updates | P3 | Func | Comp |
| AUTH-008 | Inactive user cannot log in | `qa.inactive` (inactive in its only workspace) | Login with the correct password | 401 with the same generic message as wrong credentials (no "inactive" disclosure; UX's "locked or inactive" text conflicts, see mismatches) | P1 | Neg | API |
| AUTH-009 | Rate limit: 429 | A-A1: 5/min per IP+email | 6 failed logins within 60 s | 6th → 429 "Too many attempts. Please wait a minute and try again." with `Retry-After`; the UI shows the retry time (§8 #14) | P1 | Sec | API + E2E |
| AUTH-010 | Rate limit is per IP+email | — | 5 fails for user X, then a correct login for user Y from the same IP | Y succeeds; a distributed attack (many IPs, one email) is caught by the lockout (AUTH-011) | P2 | Sec | API |
| AUTH-011 | Lockout after 5 failures for 15 minutes (DECIDED) | Fake clock | 5 wrong passwords (spaced to avoid the rate limit), then the correct password | The 5th failure locks the account; the next attempts return the **same generic locked message with the retry time** (e.g. "Too many attempts. Try again in 15 minutes." + `retryAfterSeconds` in ProblemDetails and `Retry-After`); at +14:59 still locked; at +15:00 the correct password works; the lockout is audited | P1 | Sec | API + E2E |
| AUTH-012 | Lockout counter resets on success | — | 4 fails, 1 success, 4 fails | Not locked (the counter reset after the success) | P2 | Func | API |
| AUTH-012a | Lockout message does not enumerate accounts | — | 5 wrong passwords for an **unknown** email vs a known email | Byte-identical status, body shape and retry time for both (unknown emails get a simulated lock) | P1 | Sec | API |
| AUTH-012b | Correct password during lockout does not leak | Account locked | Enter the correct password | The same generic locked message and retry time as a wrong password; the lock is not extended or reset by the correct password **[ASSUMPTION]** | P1 | Sec | API |
| AUTH-012c | Retry time is accurate and counts down | Locked at T | Attempt at T+5 min; check UI | Message/`Retry-After` ≈ 10 min; UI countdown ticks and enables Sign in at 0 (`login-retry-after`) | P2 | Func | API + E2E |
| AUTH-012d | Lockout and rate limit interplay | — | Trigger the 429 rate limit and the lockout on the same email | Both messages generic with a retry time; the longer wait wins; no difference reveals whether the account exists | P2 | Sec | API |
| AUTH-013 | Password hash algorithm | — | Inspect `AspNetUsers.PasswordHash` | Identity V3 PBKDF2 (or Argon2), never plaintext; each user has a unique salt | P1 | Sec | API |
| AUTH-014 | Workspace resolution at login (DECIDED, see 4g) | One email in Org A and Org B | Log in on `demo.<domain>`, on `iso-b.<domain>`, and on the bare domain | Subdomain → that workspace directly; bare domain → picker (WS-002); a token for A is never issued from B's host | P1 | Sec | API |
| AUTH-015 | Return URL after login | §8 #1 | Unauthenticated, open `/finance/journals` → redirected to `/auth/login?returnUrl=%2Ffinance%2Fjournals` → log in | Lands on `/finance/journals`. An open redirect such as `returnUrl=https://evil.com` or `//evil.com` → lands on `/` | P1 | Sec | E2E |
| AUTH-016 | Temporary password from an admin reset | §4.1/§4.26 | Admin resets the password; the user logs in with the temp one | Forced to change the password before any other route **[ASSUMPTION]**; the old refresh tokens are revoked (A-A4) | P2 | Func | API + E2E |
| AUTH-017 | Login audit | — | Successful and failed logins | `LastSignInAt` updated on success (§4.26); auth events logged with IP; no password in logs (check Seq) | P2 | Sec | API |
| AUTH-019 | "Keep me signed in" (DECIDED, in scope) | — | Log in ticked; log in unticked; inspect `Set-Cookie` and the RefreshToken row | Ticked: persistent cookie `Max-Age` = 30 days and the DB expiry = login + 30 days; unticked: **session cookie** (no Expires/Max-Age) and a 12 h absolute server expiry (DECIDED); default state unticked **[ASSUMPTION]** | P1 | Func | API |
| AUTH-018 | Login page a11y | — | axe scan of `/auth/login` | 0 serious or critical violations; the form works by keyboard only | P2 | A11y | E2E |

### 4b. Access token and refresh

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| AUTH-020 | Expired access token → 401 | Fake clock | Use a token at +5 min 1 s (allow ≤ 30 s ClockSkew; assert the configured skew) | 401 with `WWW-Authenticate: Bearer error="invalid_token"` | P1 | Sec | API |
| AUTH-021 | Silent refresh on 401 (interceptor) | — | Angular: expire the token; trigger an API call | The interceptor calls `/auth/refresh` once, retries the original request, and the user sees nothing | P1 | Func | Comp + E2E |
| AUTH-022 | Concurrent 401s refresh only once | — | Fire 5 parallel requests with an expired token | Exactly 1 `/auth/refresh` call (single-flight); all 5 retried successfully | P1 | Func | Comp |
| AUTH-023 | Refresh rotation | Valid cookie R1 | POST `/auth/refresh` | 200 with a new access token and a new cookie R2; R1 is marked replaced in the DB (hashed, not plaintext) | P1 | Sec | API |
| AUTH-024 | Refresh reuse detection | R1 rotated to R2 | Replay R1 | 401; **the whole family (R2) is revoked** (A-A2); the next use of R2 → 401; a security event is logged | P1 | Sec | API |
| AUTH-025 | Refresh expiry: 30-day absolute (DECIDED) | Fake clock; ticked login; refresh used daily | POST `/auth/refresh` at +30 days 1 s from login | 401 (rotation never extended the lifetime); cookie cleared; UI → `/auth/login` | P1 | Func | API |
| AUTH-026 | Refresh without a cookie | — | POST `/auth/refresh` with no cookie | 401 | P1 | Neg | API |
| AUTH-027 | Refresh with a forged or random cookie | — | Send a random value | 401; no 500 | P1 | Sec | API |
| AUTH-028 | Refresh token bound to its org and user | — | Use Org A's cookie on Org B's host | 401 | P2 | Sec | API |
| AUTH-029 | Hard reload of a deep URL keeps the session | §8 #1, §7.3 "refresh on app start" | Log in, navigate to `/finance/journals`, press F5 | `/finance/journals` renders with no blank page and no `/auth/login` redirect; exactly 1 refresh call is made at bootstrap (APP_INITIALIZER) | P1 | Func | E2E |
| AUTH-030 | Refresh reflects updated perms (`ver`) | — | Change the role, then refresh | The new token has the new `perms` and `ver` | P1 | Sec | API |
| AUTH-031 | CSRF on the refresh/logout cookie endpoints | A-A5 | Cross-site POST to `/auth/refresh` from another origin (no antiforgery or custom header) | Blocked (400/403); SameSite=Strict also prevents the cookie from being sent | P1 | Sec | API |
| AUTH-032 | CORS policy | — | Preflight from `https://evil.example` | No `Access-Control-Allow-Origin` for unknown origins; credentials only for configured origins | P1 | Sec | API |

### 4c. JWT tampering

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| AUTH-040 | `alg: none` | — | Header `{"alg":"none"}`, empty signature | 401 | P1 | Sec | API |
| AUTH-041 | Algorithm confusion | RS256 in use (if HS256, test RS-key-as-HMAC) | Sign with HS256 using the public key as the secret | 401; `ValidAlgorithms` is pinned | P1 | Sec | API |
| AUTH-042 | Signature stripped or modified | — | Change one payload char and keep the signature | 401 | P1 | Sec | API |
| AUTH-043 | Wrong `iss`/`aud` | — | A token signed by a test key with a wrong issuer or audience | 401 | P1 | Sec | API |
| AUTH-044 | `kid`/`jku`/`x5u` injection | — | A header pointing to an attacker JWKS URL | Ignored; 401 | P2 | Sec | API |
| AUTH-045 | Missing claims | — | A valid signature without `org` or `ver` (test-key factory) | 401; the tenant resolver fails closed | P1 | Sec | API |
| AUTH-046 | Token via query string is not accepted | — | `?access_token=` on REST endpoints | 401 (allowed only on the SignalR hub path, over TLS) | P2 | Sec | API |

### 4d. Logout, token storage and multi-tab

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| AUTH-050 | Logout revokes the refresh token and denylists the access token | Logged in | Avatar → "Sign out" (§4.31) → POST `/auth/logout` | 204; the DB refresh token is revoked; the access token's `jti` is in the denylist; the cookie is cleared (`Max-Age=0`); redirect to `/auth/login`; Back button does not show protected data | P1 | Func | E2E + API |
| AUTH-051 | Refresh after logout fails | Captured cookie before logout | POST `/auth/refresh` with the old cookie | 401 | P1 | Sec | API |
| AUTH-052 | Access token after logout is rejected immediately (DECIDED) | — | Use the in-memory access token captured before logout, 1 s after logout | 401 (`jti` denylisted), not valid until `exp` | P1 | Sec | API |
| AUTH-053 | No tokens in web storage | — | After login, evaluate `localStorage`, `sessionStorage`, `document.cookie`, IndexedDB | No access or refresh token; the refresh cookie is not readable by JS (HttpOnly) | P1 | Sec | E2E |
| AUTH-054 | XSS cannot reach tokens | — | Inject `<img src=x onerror=alert(1)>` into a display field (customer name) and view it | Rendered as text (Angular sanitization); CSP blocks inline script; no `bypassSecurityTrust*` (lint) | P1 | Sec | E2E + Comp |
| AUTH-055 | Security headers | — | GET `/` and `/api/v1/me` | CSP (no `unsafe-inline` for scripts), HSTS, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `frame-ancestors 'none'` | P1 | Sec | API |
| AUTH-056 | Multi-tab: session sharing | 2 tabs | Log in in tab 1; open the app in tab 2 | Tab 2 silently refreshes via the cookie and is logged in | P2 | Func | E2E |
| AUTH-057 | Multi-tab: logout propagates | 2 tabs | Sign out in tab 1 | Tab 2 detects it (BroadcastChannel/storage event) and goes to `/auth/login` within ≤ 2 s, or its next API call fails → `/auth/login` | P2 | Func | E2E |
| AUTH-058 | Multi-tab: concurrent refresh does not trigger reuse detection | Both tabs' access tokens expire at the same moment | Both refresh at once | No false reuse lockout: a cross-tab lock or a short grace window for the immediately previous token **[ASSUMPTION: 10 s grace]**; both tabs stay logged in | P1 | Func | E2E + API |
| AUTH-059 | Change password revokes other sessions | §4.31 | Change the password in browser A | Browser B's refresh → 401; A stays logged in with a new token | P2 | Sec | API + E2E |
| AUTH-060 | Logout without a cookie is idempotent | — | POST `/auth/logout` with no cookie | 204; no 500 | P3 | Neg | API |

### 4e. jti denylist (DECIDED: 5-min access token, denylist on logout)

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| AUTH-061 | Logged-out token rejected on every API instance | 2+ API instances (WebApplicationFactory ×2 or 2 containers) sharing the denylist store (Redis) | Log in via instance 1; log out via instance 1; call GET `/me` with the old token on instance 1 and on instance 2 | 401 on both, immediately (no cache propagation delay) | P1 | Sec | API |
| AUTH-062 | Denylist entry expires with the token | Fake clock + Redis | Log out; inspect the denylist key TTL; advance past `exp` | TTL ≤ remaining token lifetime (≤ 5 min); after expiry the key is gone and the token is still rejected (by `exp`) | P2 | Func | API |
| AUTH-063 | Logout-all revokes every session | User logged in on 3 devices (3 refresh families, 3 live access tokens) | POST `/auth/logout-all` **[ASSUMPTION: endpoint name]** from device 1 | All 3 access tokens → 401 immediately (all jtis denylisted, or a per-user "revoked-before" timestamp); all 3 refresh tokens → 401; audited | P1 | Sec | API |
| AUTH-064 | Replaying an old jti fails | Token T1 (jti J1) logged out; user logs in again (T2) | Call the API with T1; re-sign a token with jti J1 using a test key | T1 → 401; forged → 401 (signature); T2 works; T2's jti ≠ J1 | P1 | Sec | API |
| AUTH-065 | Every token has a unique jti | — | Issue 1,000 tokens (login + refresh) | All `jti` unique (GUID/128-bit); a token without `jti` → 401 | P1 | Sec | API |
| AUTH-066 | Denylist store unavailable fails closed (DECIDED) | Redis paused (Testcontainers) | Call an authenticated endpoint with a valid token | 401/503, never served unchecked; logged and alerted; recovers when Redis returns | P1 | Sec | API |
| AUTH-067 | Denylist check is cheap | — | k6: 200 RPS authenticated GET with the denylist enabled vs disabled | p95 overhead < 5 ms | P3 | Perf | Perf |
| AUTH-068 | SignalR connection closed on logout | Hub `/hubs/notifications` connected | Log out | The hub connection is closed or its next message is refused; a reconnect with the old token → 401 | P2 | Sec | API |
| AUTH-069 | Silent refresh at 5 min | — | Stay idle 6 min in the UI, then click a list | One refresh, request succeeds, no visible logout | P1 | Func | E2E |

### 4f. Keep me signed in (DECIDED)

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| AUTH-070 | Ticked: survives a browser restart | — | Log in ticked; close the browser (new context with only persistent cookies); open a deep URL | Logged in silently on the deep URL | P1 | Func | E2E (`keep-signed-in.spec.ts`) |
| AUTH-071 | Unticked: browser close ends the session | — | Log in unticked; close the browser (drop session cookies); open a deep URL | Redirected to `/auth/login` | P1 | Func | E2E (`keep-signed-in.spec.ts`) |
| AUTH-072 | Rotation keeps the chosen persistence and the original maximum (DECIDED) | — | Ticked and unticked logins; force refreshes | Rotated cookie keeps its type; persistent cookie keeps the original 30-day absolute expiry; session-only keeps the original 12 h server maximum | P1 | Func | API |
| AUTH-073 | Unticked session has a 12 h absolute server maximum (DECIDED) | Unticked login, browser kept open, refreshes every 5 min | Advance the fake clock to +11:59 and +12:00:01 | Refresh works at 11:59; 401 at 12:00:01 | P1 | Sec | API |
| AUTH-074 | Logout and logout-all end persistent sessions | Ticked login on 2 devices | Logout on device 1; logout-all from device 1 | Device 1 cookie cleared, token revoked; after logout-all device 2's persistent cookie → 401 | P1 | Sec | API |
| AUTH-075 | Password change or admin reset revokes persistent sessions | Ticked login | Change the password elsewhere; admin reset | The persistent refresh → 401 | P1 | Sec | API |
| AUTH-076 | Checkbox UX and a11y | — | Keyboard toggle; tooltip text | Tooltip says 30 days (the UX doc still says 14 days, G-UX-1); label associated; `data-testid="login-keep-signed-in"` | P3 | Func | Comp |

### 4g. Multiple workspaces per email (DECIDED v0.3)

Decision (b): one email can belong to several workspaces. Model assumed for testing: one global identity (email + password + lockout state) with a **membership** per workspace (roles, entity access, active flag) **[ASSUMPTION A-T3']**. Seed: `qa.multi@example.test` is Org admin in Org A and Viewer in Org B; `qa.single@…` is a member of Org A only.

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| WS-001 | Exactly one workspace: auto-select | `qa.single` | Log in at the bare login URL (no subdomain) | No picker; the token's `org` = A; lands on `/` | P1 | Func | API + E2E |
| WS-002 | Several workspaces: picker after login | `qa.multi` | Log in at the bare login URL | Picker lists exactly A and B (display name + workspace address), no other workspaces; until a choice is made only a short-lived pre-auth token exists, which is rejected by every API except the picker endpoints | P1 | Func | API + E2E |
| WS-003 | Picking issues tokens scoped to that workspace | WS-002 | Pick B | Access token `org` = B, `perms` = B's Viewer keys, `ents` = B's entities; refresh cookie bound to B; lands on `/` (or the return URL) | P1 | Sec | API + E2E |
| WS-004 | Cannot pick a workspace you don't belong to | Pre-auth token of `qa.multi` | POST `/auth/select-workspace` **[ASSUMPTION: endpoint]** with Org C's id; a random GUID; Org B after the membership is deactivated | Generic 403/404 for all three (no disclosure that C exists); no tokens issued | P1 | Sec | API |
| WS-005 | Pre-auth token is short-lived and single-use | — | Use it after its TTL; use it twice; tamper its `sub` | 401 each time | P1 | Sec | API |
| WS-006 | Switching workspace issues new scoped tokens | Signed in to A | POST `/auth/switch-workspace {orgId:B}` **[ASSUMPTION: endpoint name]**; revocation of A's tokens DECIDED | New access + refresh tokens for B; the A access token's `jti` is denylisted and the A refresh token revoked **[ASSUMPTION]**; old A token → 401 | P1 | Sec | API |
| WS-007 | No data bleed after switching | `qa.multi` Org admin in A, Viewer in B | Switch A → B; GET lists, `/dashboard`, `/search`, reports, `/me` | Only B data; KPI values are B's; caches keyed by (user, org) | P1 | Sec | API |
| WS-008 | No permission bleed after switching | Same | In B, POST `/customers`, PUT `/settings` | 403 (Viewer in B), although the user is Org admin in A | P1 | Sec | API |
| WS-009 | UI state is reset on switch | E2E | Switch workspace in the UI | Entity switcher shows B's entities; list/stores and SignalR groups are reset; no A rows remain visible | P1 | Func | E2E |
| WS-010 | Subdomain resolves the workspace | `qa.multi` | Log in at `a.<domain>` and at `c.<domain>` (not a member) | `a.` → straight into A, no picker; `c.` → the generic invalid-credentials message, not revealing the other memberships | P1 | Sec | API + E2E |
| WS-011 | Membership list only after a successful password check | — | Wrong password for `qa.multi` | Generic 401; no workspace names in the response | P1 | Sec | API |
| WS-012 | Inactive in one workspace only | `qa.multi` deactivated in B | Log in | Auto-selects A (only active membership) or the picker shows B as unavailable; selecting B → 403 | P2 | Func | API |
| WS-013 | Lockout and password are per identity | — | 5 failures on `a.` then log in on `b.` | Locked on both (one identity); a password change applies to all workspaces and revokes sessions in all of them | P1 | Sec | API |
| WS-014 | Same email unique within a workspace only | — | Invite `qa.multi` to Org C; invite them again to Org A | C: a new membership, password unchanged; A: 409 already a member | P2 | Func | API |
| WS-015 | Workspace events are audited in the right workspace | — | Select or switch to B | The event appears in B's audit log only | P2 | Sec | API |
| WS-016 | Multi-tab after a switch | Tab 1 in A, tab 2 switches to B | Tab 1 makes a call | Tab 1 detects the workspace change and reloads, or is signed out; it never shows A screens with B data | P2 | Func | E2E |
| WS-017 | Return URL survives the picker | — | Open `/finance/journals` signed out; log in; pick B | Lands on `/finance/journals` in B | P3 | Func | E2E |

### 4h. Password reset tokens (DECIDED v0.3: single-use, 30 minutes)

**Invalidation rule (DECIDED):** requesting a new reset link **invalidates all earlier unused tokens** for that identity, and a successful reset invalidates every outstanding token.

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| PWR-001 | Generic response, no account enumeration | — | POST `/auth/forgot` for a known active email, an unknown email, an inactive user, and a multi-workspace email | Same status (202), same body and similar timing (median ±50 ms over 20 tries); an email is sent only to the known active identity; the UI always shows the same confirmation (02-password-recovery C1) | P1 | Sec | API + E2E |
| PWR-002 | Token is single-use | Valid token | POST `/auth/reset {token, password}`; repeat with the same token | First 200; second 400 with the generic "This link has expired or was already used." | P1 | Sec | API |
| PWR-003 | Token expires after 30 minutes | Fake clock | Use at +29:59 and (a fresh token) at +30:01 | First works; second → the same generic 400 | P1 | Sec | API |
| PWR-004 | A new request invalidates the old token | Token T1 issued, then T2 requested | Use T1; then T2 | T1 → generic 400; T2 → 200 | P1 | Sec | API |
| PWR-005 | Successful reset invalidates other outstanding tokens | T1 and T2 exist (e.g. from different devices before PWR-004 applies) | Reset with T2; then use T1 | T1 → 400 | P1 | Sec | API |
| PWR-006 | Token validate endpoint does not consume the token | — | GET `/auth/reset/validate?token=` (UX proposed) twice, then POST reset | Validate 200 twice; reset 200 | P2 | Func | API |
| PWR-007 | Token strength and storage | — | Inspect the token and the DB | ≥ 128-bit random, URL-safe; stored hashed; not written to logs (Seq query); reset page sends `Referrer-Policy: no-referrer` | P1 | Sec | API |
| PWR-008 | Token bound to its identity | T for user X | POST reset with T and `email` of Y in the body; tamper the token | Only X's password changes / 400 | P1 | Sec | API |
| PWR-009 | Reset always signs out other devices (DECIDED) | X signed in on devices 1 and 2 (refresh tokens R1, R2; access tokens J1, J2) | Reset the password on device 3 | POST `/auth/refresh` with R1 and R2 → 401; API calls with J1 and J2 → 401 immediately on every instance (jtis denylisted); device 3 signs in with the new password only | P1 | Sec | API |
| PWR-009a | No opt-out of signing out other devices (DECIDED) | X signed in on device 1 | POST `/auth/reset-password` with a valid token plus `{"signOutOtherDevices": false}` (and `keepSessions: true`) | 200 (or 400 unknown field); the opt-out is ignored: device 1's refresh token and jti are rejected exactly as in PWR-009 | P1 | Sec | API |
| PWR-009b | Reset UI has no "Sign me out of other devices" checkbox (DECIDED) | Valid reset link | Open `/auth/reset-password?token=…` | No such checkbox or toggle; copy may state that other devices will be signed out | P2 | UI | E2E (`password-reset.spec.ts`) |
| PWR-010 | Reset link host is not taken from the request | — | POST `/auth/forgot` with `Host: evil.example` / `X-Forwarded-Host` | The email link uses the configured base URL (no host-header injection) | P1 | Sec | API |
| PWR-011 | Forgot endpoint is rate-limited without revealing anything | — | 20 requests per minute for one email/IP | 429 after the limit, with the same message for known and unknown emails; resend cooldown 30 s (UX) | P2 | Sec | API |
| PWR-012 | Reset applies to the identity across workspaces | `qa.multi` | Reset | The new password works in A and B | P2 | Func | API |
| PWR-013 | Reset clears a lockout | Locked account | Complete a reset | Login works immediately **[ASSUMPTION]** | P2 | Func | API |
| PWR-014 | UI states for a used or expired link | — | Open the reset page with a used or expired token | "This link has expired or was already used." + **Request a new link** (UX 02-B) | P2 | Func | E2E |

### 4i. Password policy (DECIDED v0.3: ≥ 10 characters, upper case, lower case, a digit)

Surfaces: **registration** (setting the first password via invite acceptance or the Add-user initial password; there is no public sign-up, UX Q2), **reset** (`/auth/reset`), **change** (`/auth/change-password`, including the forced change after a temporary password). The server enforces the policy on every surface; the UI checklist (UX 02-B) mirrors it.

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| PWP-001 | Registration: 9 characters (below the minimum) | Valid invite token | Submit new password `Abcdefgh1` (and the matching confirmation) | 400 "at least 10 characters"; no password change; error mapped to the field | P1 | Neg | API (theory)  |
| PWP-002 | Registration: 10 characters, all classes (boundary) | Valid invite token | Submit new password `Abcdefgh12` (and the matching confirmation) | Accepted; error mapped to the field | P1 | Neg | API (theory)  |
| PWP-003 | Registration: Missing upper case | Valid invite token | Submit new password `abcdefgh12` (and the matching confirmation) | 400 "an upper-case letter"; no password change; error mapped to the field | P1 | Neg | API (theory)  |
| PWP-004 | Registration: Missing lower case | Valid invite token | Submit new password `ABCDEFGH12` (and the matching confirmation) | 400 "a lower-case letter"; no password change; error mapped to the field | P1 | Neg | API (theory)  |
| PWP-005 | Registration: Missing a digit | Valid invite token | Submit new password `Abcdefghij` (and the matching confirmation) | 400 "a digit"; no password change; error mapped to the field | P1 | Neg | API (theory)  |
| PWP-006 | Reset: 9 characters (below the minimum) | Valid reset token | Submit new password `Abcdefgh1` (and the matching confirmation) | 400 "at least 10 characters"; no password change; error mapped to the field | P1 | Neg | API (theory) + Comp |
| PWP-007 | Reset: 10 characters, all classes (boundary) | Valid reset token | Submit new password `Abcdefgh12` (and the matching confirmation) | Accepted; error mapped to the field | P1 | Neg | API (theory) + Comp |
| PWP-008 | Reset: Missing upper case | Valid reset token | Submit new password `abcdefgh12` (and the matching confirmation) | 400 "an upper-case letter"; no password change; error mapped to the field | P1 | Neg | API (theory) + Comp |
| PWP-009 | Reset: Missing lower case | Valid reset token | Submit new password `ABCDEFGH12` (and the matching confirmation) | 400 "a lower-case letter"; no password change; error mapped to the field | P1 | Neg | API (theory) + Comp |
| PWP-010 | Reset: Missing a digit | Valid reset token | Submit new password `Abcdefghij` (and the matching confirmation) | 400 "a digit"; no password change; error mapped to the field | P1 | Neg | API (theory) + Comp |
| PWP-011 | Change: 9 characters (below the minimum) | Signed in; correct current password | Submit new password `Abcdefgh1` (and the matching confirmation) | 400 "at least 10 characters"; no password change; error mapped to the field | P1 | Neg | API (theory) + Comp |
| PWP-012 | Change: 10 characters, all classes (boundary) | Signed in; correct current password | Submit new password `Abcdefgh12` (and the matching confirmation) | Accepted; error mapped to the field | P1 | Neg | API (theory) + Comp |
| PWP-013 | Change: Missing upper case | Signed in; correct current password | Submit new password `abcdefgh12` (and the matching confirmation) | 400 "an upper-case letter"; no password change; error mapped to the field | P1 | Neg | API (theory) + Comp |
| PWP-014 | Change: Missing lower case | Signed in; correct current password | Submit new password `ABCDEFGH12` (and the matching confirmation) | 400 "a lower-case letter"; no password change; error mapped to the field | P1 | Neg | API (theory) + Comp |
| PWP-015 | Change: Missing a digit | Signed in; correct current password | Submit new password `Abcdefghij` (and the matching confirmation) | 400 "a digit"; no password change; error mapped to the field | P1 | Neg | API (theory) + Comp |
| PWP-016 | All rules reported together | — | Submit `abc` on each surface | One 400 listing all unmet rules (length, upper, digit) | P2 | Neg | API |
| PWP-017 | Server enforces the policy when the client is bypassed | — | Call the API directly with `Abcdefgh1` on each surface | 400 (not only client-side) | P1 | Sec | API |
| PWP-018 | Admin temporary password meets the policy | Admin resets a user (§4.26) | Inspect the generated temporary password | ≥ 10 characters with upper, lower and a digit; a forced change follows (AUTH-016) | P2 | Func | API |
| PWP-019 | Maximum length 128 (DECIDED) | — | On each surface: a 128-character policy-compliant password; 129 characters; `Abcdefgh1 ` (trailing space, 10 chars) | 128 accepted and the full string logs in (no truncation); 129 → 400 "at most 128 characters"; spaces count as characters | P1 | Neg | API (theory) |
| PWP-020 | Any Unicode letter counts for upper/lower (DECIDED) | — | `Ébcdefgh12` (É upper), `ÄÖÜabcde12`, `Ωmegapass1` (Greek upper), `ábcdefgh12` (no upper), `ÉÀÇDEFGH12` (no lower), `Abcdefghİ1` | First three accepted; `ábcdefgh12` → 400 upper; `ÉÀÇDEFGH12` → 400 lower; Turkish İ counts as upper (accepted); server and UI checklist agree | P1 | Neg | API + Comp |
| PWP-023 | Password equal to the email is rejected (DECIDED) | Email `qa.Owner1@example.test` (meets length/classes) | On registration, reset and change: password = `qa.Owner1@example.test` | 400 "must not be the same as your email"; no change | P1 | Neg | API (theory × 3) |
| PWP-024 | Case-variant of the email is rejected (DECIDED, case-insensitive) | Same | On each surface: `QA.owner1@Example.Test` (a case variant that still meets the class rules) | 400 on all three surfaces; the UI checklist item "Not the same as your email" shows not met | P1 | Neg | API + Comp |
| PWP-025 | Password that only contains the email passes | Same | On each surface: `qa.Owner1@example.testX` and `Xqa.Owner1@example.test` | Accepted (the rule is equality only; spec and UX say "not equal", not "not contain") | P2 | Func | API (theory × 3) |
| PWP-021 | UI checklist mirrors the server | — | Type 9 then 10 characters, remove the digit, etc. on reset and change pages | Checklist items flip at the same boundaries as the server (UX 02-B); the button is disabled until all pass | P2 | Func | Comp + E2E |
| PWP-022 | Existing passwords are not retro-validated | User with an old 8-character password (seeded before the policy) | Log in | Login works; the next change must meet the policy **[ASSUMPTION]** | P3 | Func | API |

---

## 5. Industry-agnostic core guards

Hard constraint (Muhammad, 22 Oct 2026): the product must not be tied to one business type. See strategy §1a and CI gate 13. "Sample module" = the test-only `ERP.Modules.Sample` (entity `SampleItem`, perms `sample.read/write`, one menu item, one route, one job).

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| CORE-001 | Core works end to end with zero modules | API host built and started with **no module assemblies** (`ERP_MODULES=`) | Create an org (onboarding, "Generic / no industry") → create a legal entity → create users → assign roles → log in → refresh → change settings → create a General approval and approve it as another user → view the audit log → log out | Every step succeeds; no 500s; no references to missing module services in the logs | P1 | Func | API + E2E |
| CORE-002 | Core test suite runs green with zero modules | CI job `core-only` | Run Domain, Application and API integration tests (core traits) without module assemblies | Green (CI gate 13a) | P1 | Arch | API |
| CORE-003 | Core does not reference module assemblies | — | NetArchTest: core assemblies `ShouldNot().HaveDependencyOnAny("ERP.Modules.*")`; also check `.csproj` ProjectReferences | Pass; fails if any core project references a module | P1 | Arch | Unit |
| CORE-004 | Angular core does not import modules | — | ESLint `no-restricted-imports` / Nx boundary rule over `src/app/core/**`, `shared/**`, `layout/**` | No import from `modules/**` | P1 | Arch | Unit (lint) |
| CORE-005 | Core permissions without modules | Zero modules | GET `/me` as Org admin; GET `/roles` | Only `core.*` and `finance.*` keys; no `realestate.*`, `hr.*` or `sample.*` keys (`finance.invoices/receipts/bills/payments` **are** core) | P1 | Func | API |
| CORE-006 | Core seed has only generic roles (DECIDED) | Zero modules, no industry template | GET `/roles` | Core roles = Org admin, Finance manager, Accountant, Approver, Viewer; "Leasing manager", "Facilities manager" and "Brokerage manager" are absent (they come from the RE industry template, CORE-029) | P1 | Func | API |
| CORE-007 | Enabling or disabling Real Estate does not change core behaviour | Snapshot core responses (`/me` core part, `/users`, `/roles` core roles, `/settings` core section, `/accounts`, `/audit` shape) | Enable `realestate` → re-snapshot → disable → re-snapshot | Core snapshots are identical apart from module-contributed additions explicitly flagged as module-owned | P1 | Func | API |
| CORE-008 | Enabling or disabling the Sample module does not change core behaviour | — | Same as CORE-007 with `sample` | Same | P1 | Func | API |
| CORE-009 | Core data is untouched by a module toggle | Checksums of core tables (Organization, LegalEntity, User, Role, Account, JournalEntry, AuditEvent excluding the toggle event) | Toggle `realestate` and `sample` on and off | Checksums are unchanged | P1 | Func | API |
| CORE-010 | Core API contract is stable across module states | OpenAPI JSON | Generate OpenAPI with no modules, with RE only, with Sample only, and with both; diff the **core** paths and schemas | Core paths and schemas are byte-identical; module paths are only under `/api/v1/m/{key}` (A-M10) | P1 | Contract | API |
| CORE-011 | Module toggle in org A does not affect org B: data | A and B both have `realestate` enabled | Disable it in A | B's RE endpoints return 200 with the same data | P1 | Sec | API |
| CORE-012 | ...menus and routes | Same | B user logs in after A disables it | B's sidebar still shows the RE items; `/real-estate/leases` works for B; A's user sees the "module not enabled" page | P1 | Sec | E2E |
| CORE-013 | ...permissions | Same | GET `/me` for `qa.leasing` in B | `realestate.leases.*` still effective in B | P1 | Sec | API |
| CORE-014 | ...caches | Module state cached per org (A-M7) | Warm B's module-state and menu cache; toggle in A; read B | B's cache entry is not invalidated or overwritten; keys include OrgId | P1 | Sec | API |
| CORE-015 | ...jobs | — | Disable RE in A; run the lease-status job | A skipped; B processed | P1 | Sec | API |
| CORE-016 | No RE terms in the core schema | — | Vocabulary lint over core migrations and the core DbContext model (table, column and enum names) against `core-vocabulary-denylist.txt` | 0 hits (see also CORE-027) | P1 | Arch | Unit |
| CORE-017 | No RE terms in core seed data (DECIDED) | — | Scan the core seeder output (roles, permissions, settings, numbering prefixes) | 0 hits. RE CoA accounts (4000 "Rental income", 2200 security deposits, 5400 "Utilities (DEWA/ADDC/chiller)"), RE roles and the LSE/PROP/WO/DEAL prefixes come only from the RE industry template (CORE-028/029). PDC/cheque prefixes are core (cheques are core finance) | P1 | Arch | Unit |
| CORE-018 | No RE terms in core UI labels and default navigation | Zero modules | Scan `i18n/core/*.json`; render the shell; read the sidebar | Sidebar = Overview, Approvals, Finance (CoA, Journals, Reports), Administration only; Overview has no portfolio, occupancy or rent KPIs; no "the reference demo" product branding in the core shell (configurable product name) | P1 | Func | E2E + Unit |
| CORE-019 | No hardcoded industry entities in core code | — | Grep or Roslyn over core assemblies for type names from the denylist (`Lease`, `Unit`, `Property`, `Cheque`…) | 0 hits | P1 | Arch | Unit |
| CORE-020 | Dashboard is composable | Zero modules, then RE | GET `/dashboard` | Core-only returns generic widgets (cash, approvals, AR/AP if those modules are on); RE adds portfolio widgets via registration | P2 | Func | API |
| CORE-021 | Onboarding is parameterized by industry | Industries: realestate, retail, manufacturing, services, distribution, hr-only (BA §4), generic (Playwright: `tests/data/industries.ts`) | For each: POST `/onboarding {orgName, signInKey, industry, country}`, then GET `/modules` and `/me` | Valid org, admin user and core CoA every time; the enabled module set equals the industry bundle (A-M9); for retail/manufacturing/services the unavailable industry modules are listed but not enabled; generic = core + no industry module | P1 | Func | API (theory) + E2E |
| CORE-022 | Onboarding rejects an unknown industry | — | `industry: "casino"` | 400 with a list of allowed values | P2 | Neg | API |
| CORE-023 | Onboarded orgs are isolated from each other | Run CORE-021 for 5 industries | Cross-call each org's ids from the others | 404 everywhere (§1) | P1 | Sec | API |
| CORE-024 | Module install and remove need no core migration | Record `__EFMigrationsHistory` (core) and the core schema hash | Install, enable, disable and uninstall `sample`, then `realestate` | Core history rows and schema hash are unchanged; module tables are only in the module schema/history (`re.`, `__EFMigrationsHistory_RealEstate`) | P1 | Arch | API |
| CORE-025 | Uninstalling a module leaves core consistent | `sample` has data and posted journals | Uninstall `sample` (data policy per A-M3: retained) | Core reports (trial balance) still balance and include historic journals; no FK from core to module tables blocks it | P2 | Func | API |
| CORE-027 | Core Organization has no lease columns (DECIDED) | Migrated empty DB | Inspect `Organization` and every core table; GET `/settings` | No `LeaseExpiryAlertDays` or other lease columns in core; `chequeDueAlertDays` (default 7, §4.25) **is** a core finance setting in `/settings`; PUT `/settings` with `leaseExpiryAlertDays` → 400 unknown field | P1 | Arch | API |
| CORE-028 | Core seed has no RE roles or chart of accounts (DECIDED) | New workspace, "Other / General business", zero modules | GET `/roles`, GET `/accounts` | No RE roles (CORE-006); the CoA is the generic core/country set with no RE accounts (no rental income, security deposits, chiller/DEWA/ADDC) | P1 | Func | API |
| CORE-029 | Applying the RE industry template gives the RE roles and CoA (DECIDED) | New workspace | Apply the RE template (onboarding "Real estate" or POST `/m/realestate/template/apply` **[ASSUMPTION: endpoint]**) | Roles Leasing manager, Facilities manager, Brokerage manager are created with the `realestate.*` grants of the matrix; the RE CoA accounts from spec §4.17 are added; RE numbering prefixes registered; RE settings created with default `leaseExpiryAlertDays=60` (§4.25) | P1 | Func | API |
| CORE-030 | Old flat module paths return 404 (DECIDED) | RE and HR enabled | GET/POST `/properties`, `/units`, `/leases`, `/acquisitions`, `/work-orders`, `/deals`, `/billing-runs`, `/employees`, `/payroll-runs`, `/reports/rent-roll` (no `/m/{key}` prefix) | 404 for every one (no redirect, no alias); the namespaced equivalents return 200; core `/invoices`, `/receipts`, `/bills`, `/payments` return 200 with **zero modules** | P1 | Contract | API |
| CORE-031 | RE settings live in the module (DECIDED) | RE enabled | GET/PUT `/m/realestate/settings` with `leaseExpiryAlertDays`; disable RE | Value persists and drives lease-expiry alerts (60 d default); disabled → 403 `module-disabled`, value retained; `chequeDueAlertDays` is **not** accepted here (core finance) | P1 | Func | API |
| CORE-032 | Re-applying the template is idempotent | RE template applied once | Apply again (e.g. disable/re-enable RE, or a template upgrade) | No duplicate roles, accounts or prefixes; tenant edits to template items are kept (BA WIZ-02/03) | P2 | Func | API |
| CORE-033 | Legal entities work with no modules | Zero modules | Create, edit, lock and reopen a legal entity via `/entities` with `core.entities.write` | All succeed (DECIDED: legal entities are core) | P1 | Func | API |
| CORE-034 | Sales and purchase finance work with zero modules (DECIDED) | Zero modules | Create, issue and void a sales invoice; record a receipt and allocate it; enter, approve and pay a purchase bill; run receivables and payables ageing | All succeed under core routes and `finance.*` keys; journals balance | P1 | Func | API |
| CORE-026 | Generic terms in core API DTOs | — | Inspect core DTO property names (`counterparty`/`customer`, `document`, `legalEntity`) | No industry nouns; the RE "tenant" (lessee) is a Customer category registered by RE | P2 | Contract | Unit |

### 5a. Test-only seeding endpoint (DECIDED)

`POST /api/v1/test-support/workspaces` (provision a not-yet-onboarded workspace + first admin) and related test-support routes exist **only when `ASPNETCORE_ENVIRONMENT=Testing`**.

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| TS-001 | Endpoint returns 404 outside Testing | Host started as Development, Staging and Production (theory) | POST/GET `/api/v1/test-support/workspaces` with and without an admin token | 404 in each (not 401/403, so the route's existence is not revealed); absent from the OpenAPI document | P1 | Sec | API |
| TS-002 | Endpoint works in Testing | Host in Testing | POST `{slug, adminEmail, adminPassword}` | 201; the workspace has `setupStatus != Complete`; the admin logs in and lands on `/onboarding/company`; the password must meet the policy | P1 | Func | API + E2E |
| TS-003 | Release images cannot enable it | Production image | Start with `ASPNETCORE_ENVIRONMENT=Testing` overridden by deploy config check | The release checklist and a startup guard refuse Testing in production deployments | P2 | Sec | Manual |

---

## 5b. Cheques and PDCs as a core finance instrument (DECIDED)

Decisions 3 + final pass: cheques/PDCs are a **core finance payment instrument** with `finance.cheques.read/write`, **`finance.cheques.approve`** (checker step for deposit, clear, bounce, replace and cancel) and **`finance.cheques.issue`** (issuing an outgoing cheque), served at core `/cheques`. Clearing a cheque creates a **core receipt**; the cheque-due alert threshold is a **core finance setting**. RE only adds lease links and lease-expiry alerts. The RE module only **links** cheques to leases and instalments (the link lives in the RE module; core `Cheque` has no `LeaseId`/`InstalmentId` columns). Spec basis for the lifecycle: §4.10, §5.2 (InCustody → Deposited → Cleared / Bounced → Replaced / Returned).

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| CHQ-001 | Cheques work with RE disabled | `realestate` disabled (or never installed); `qa.finance` | Create a cheque for a customer; deposit to a bank account; clear; create another, bounce, replace | All succeed via `/cheques`; statuses follow §5.2; postings are balanced (Dr bank on clear) | P1 | Func | API |
| CHQ-002 | Cheque register UI with RE disabled | Same | Open the cheque register (Finance) | It works with no lease columns or lease filters | P1 | Func | E2E |
| CHQ-003 | Lease link is only available when RE is enabled | RE disabled | POST `/cheques` with `leaseId`/`instalmentId`; GET a cheque DTO; POST `/m/realestate/leases/{id}/cheques` | `leaseId` → 400 unknown field (or 403 `module-disabled`); DTO has no lease link section; the RE route → 403 `module-disabled` | P1 | Func | API |
| CHQ-004 | Lease link works when RE is enabled | RE enabled; an active lease with a quarterly plan | POST `/m/realestate/leases/{id}/cheques` (bulk, one per instalment, §5.1 step 4) | Core cheques are created plus RE link rows (lease, instalment); the cheque DTO shows the link under an RE extension block | P1 | Func | API |
| CHQ-005 | Disabling RE keeps cheques and link data | CHQ-004 done | Disable RE; query the DB; GET `/cheques/{id}`; re-enable RE | Cheques unchanged and usable; RE link rows intact (row count + checksum); after re-enable the links are visible again | P1 | Func | API |
| CHQ-006 | Core cheque edits don't drop the link | RE disabled, linked cheque | Deposit and clear the cheque while RE is disabled; re-enable RE | Link still present; the lease view shows the new status | P2 | Func | API |
| CHQ-007 | Core Cheque table has no lease columns | — | Inspect the core schema | No `LeaseId`/`InstalmentId` on `Cheque`; the link table is in the RE schema/migrations (CORE-024) | P1 | Arch | API |
| CHQ-008 | `finance.cheques.read/write` enforced | Matrix rows | `qa.hr` (no cheque keys) GET `/cheques`; `qa.viewer` POST `/cheques/{id}/deposit` | 403; 403 | P1 | Sec | API |
| CHQ-009 | `finance.cheques.approve` maker-checker | A cheque request needing approval (scope G-PERM-2), created by `qa.finance` | `qa.finance` approves their own; `qa.approver` approves | Own → 403/409; other → 200 | P1 | Sec | API |
| CHQ-010 | Cheques are org- and entity-scoped | Org A/B, ENT-0001/0002 | Cross-org GET by id; restricted user lists cheques | 404; only allowed entity's cheques | P1 | Sec | API |
| CHQ-011 | Clearing a cheque creates a core receipt (DECIDED) | RE disabled, zero modules | Deposit and clear a customer cheque (maker + checker) | A core receipt is created and allocated (or held unallocated/on account when no invoice is linked); journal Dr bank / Cr AR balanced (§5.1 step 7) | P1 | Func | API |
| CHQ-012 | Cheque-due alerts are core (DECIDED) | RE disabled; `chequeDueAlertDays=7` in core `/settings`; cheque dated in 3 days | Run the alerts builder | Alert raised; changing the core setting to 2 suppresses it | P1 | Func | API |
| CHQ-013 | Maker cannot approve their own cheque action | `qa.finance` holds `finance.cheques.write` + `finance.cheques.approve` | For each action deposit, clear, bounce, replace, cancel: `qa.finance` requests it, then approves it | Request → 202 pending; own approval → 403/409 "cannot approve your own request"; cheque status unchanged; no journal posted | P1 | Sec | API (theory × 5) |
| CHQ-014 | Checker approves → action applies | Pending action requested by `qa.accountant` | `qa.approver` POST `/cheques/{id}/approve` | Status moves per §5.2; postings (clear → receipt; bounce → reversal) happen only now; decider, time and comment stored | P1 | Func | API (theory × 5) |
| CHQ-015 | Approve without write cannot start an action | `qa.approver` (approve, no write) | POST `/cheques/{id}/deposit` | 403 | P1 | Sec | API |
| CHQ-016 | Write without approve cannot complete an action | `qa.leasing` (write, no approve) | Request deposit; then call `/approve` | 202; 403; the cheque stays pending | P1 | Sec | API |
| CHQ-017 | Issue an outgoing cheque | `qa.accountant` (`finance.cheques.issue`) | POST `/cheques/outgoing` for a vendor payment | 201; outgoing cheque recorded; issuing has no approval step, but its **cancel** needs the checker (CHQ-013) | P1 | Func | API |
| CHQ-018 | Issue without the issue key | `qa.leasing` (write, no issue); `qa.approver` (approve, no issue) | POST `/cheques/outgoing` | 403 for both (approve does not imply issue) | P1 | Sec | API |
| CHQ-019 | Rejected request leaves the cheque unchanged | Pending clear | Checker rejects with a comment | Status unchanged; no receipt; audit shows the rejection | P2 | Func | API |
| CHQ-020 | Bulk deposit is approved per cheque | 5 cheques in a bulk deposit request | Checker approves 3, rejects 2 | Only the 3 are Deposited; each decision audited | P2 | Func | API |

---

## 6. Custom fields

Spec basis: §9 "Custom roles and custom fields: custom fields (text, number, date, select) on properties, units, customers, leases, vendors, employees; usable in filters and exports". The new requirement adds per-org definitions with values in a JSON column. Assumptions A-CF1…A-CF4 (strategy §14). The engine is **core**; targets are any `IHasCustomFields` entity (Customer and Vendor are core-shareable; Property, Unit and Lease come from the RE module; SampleItem comes from the Sample module).

**Aligned with BA `ba/core-domain.md` §8** (CF-SEC, CF-VAL, CF-NAM, CF-LIF, CF-TYP, CF-LIM, CF-PRM, CF-PCK rules). Model: `CustomFieldDefinition` / `CustomFieldOption` per org (+ optional entity restriction); values in `CustomFields nvarchar(max)` with `CHECK (ISJSON(CustomFields)=1)`; filterable/searchable/unique fields mirrored into a `CustomFieldIndex` side table in the same transaction (BA recommendation, no runtime DDL). API keys are `cf_` + lower snake case (`cf_<module>_` for packs). API: `GET /custom-fields?recordType=` (definitions), DTOs carry `customFields: { cf_key: value }`, filters `?cf.cf_key=value`, `sort=cf.cf_key`. Manage permission `core.customfields.manage`; read `core.customfields.read` (BA's `customization.fields.*` mapped to the decided key format). Range-filter operator syntax (`cf.cf_x[gte]=`) is an SQA **[ASSUMPTION]**.

### 6a. Type validation

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| CF-001 | Text: min and max length | `cf_ref_code` text minLength 3, maxLength 20 | Save values "ab", "abc", 20 chars, 21 chars, "" (optional) | "ab" 400; "abc" ok; 20 ok; 21 400; "" stored as null | P1 | Neg | Unit + API |
| CF-002 | Text: regex | regex `^[A-Z]{3}-\d{4}$` | "ABC-1234", "abc-1234", "ABC-12345" | ok; 400; 400 (anchors enforced) | P1 | Neg | Unit |
| CF-003 | Regex definition safety (ReDoS) | — | Define regex `(a+)+$`; save "aaaa…a!" (50 chars) | Definition rejected by the validator, or evaluated with a `Regex` timeout (≤ 100 ms) → 400; the server is not blocked | P1 | Sec | Unit |
| CF-004 | Text: whitespace and unicode | — | "  x  ", Arabic "مرحبا", emoji, a zero-width char | Trimmed by the rule **[ASSUMPTION: trim]**; length counted in characters; Arabic stored and returned intact (§9 Arabic) | P2 | Func | Unit |
| CF-005 | Long text max | longtext maxLength 2000 | 2000 vs 2001 characters | ok / 400 | P2 | Neg | Unit |
| CF-006 | Number (integer) range | `cf_floors` min 0, max 200 | -1, 0, 200, 201, 1.5, "12", 1e3 | -1 400; 0 ok; 200 ok; 201 400; 1.5 400; "12" 400 (type confusion, strict JSON number); 1e3 400 (>200) | P1 | Neg | Unit + API |
| CF-007 | Number overflow | — | 9223372036854775808, NaN, Infinity | 400; no 500 | P1 | Sec | API |
| CF-008 | Decimal precision and scale | `cf_service_charge` decimal(18,2), min 0 | 10.5, 10.555, -0.01, 12345678901234567.89 | 10.50 ok; 10.555 400 (or rounded half-away-from-zero per §10.5; **choose one, test asserts 400**); -0.01 400; overflow 400 | P1 | Neg | Unit |
| CF-009 | Decimal serialization round trip | — | Save 0.1 + 0.2 style values; read back | Exact decimal string (no binary float artefacts); invariant culture (`.` separator) | P2 | Func | API |
| CF-010 | Date | `cf_handover_date` date min 2000-01-01 | "2026-10-22", "2026-02-30", "22/10/2026", "2026-10-22T10:00:00Z", 1761091200 | ok; 400; 400 (ISO only); 400 or normalized to date-only **[ASSUMPTION: 400]**; 400 | P1 | Neg | Unit |
| CF-011 | Date with time zone | Org TZ Asia/Dubai | Save "2026-10-22"; read it as a user in PKT | Same date (date-only, no TZ shift) | P2 | Func | API + Comp |
| CF-012 | Boolean | `cf_has_parking` boolean | true, false, null, "true", 1 | ok; ok; null (optional); 400; 400 | P1 | Neg | Unit |
| CF-013 | Single select: valid and invalid option | options `[gold, silver, bronze]` | "gold", "platinum", "GOLD", ["gold"] | ok; 400; 400 (case-sensitive values); 400 (an array for single select) | P1 | Neg | Unit + API |
| CF-014 | Multi select | options `[pool, gym, spa]`, maxSelections 2 **[ASSUMPTION]** | ["pool"], ["pool","gym","spa"], ["pool","pool"], [], "pool", ["x"] | ok; 400; dedup or 400 **[assert 400]**; [] = empty; 400; 400 | P1 | Neg | Unit |
| CF-014a | Currency (BA §8.3) | `cf_fee` currency fixed AED | 10.555, "10.5 USD", {amount, currency:"USD"} when fixed | 2-dp rule as CF-008; a wrong currency → 400 | P2 | Neg | Unit |
| CF-014b | Percent | min 0 max 100, decimal(9,4) | -1, 100.00001, 12.3456 | 400; 400; ok | P2 | Neg | Unit |
| CF-014c | Date-time | — | "2026-10-22T10:00:00+04:00" | Stored as UTC 06:00Z, displayed in org TZ (Asia/Dubai) | P2 | Func | Unit + Comp |
| CF-014d | Email, phone, URL | — | email "a@b", phone "0501234567" (default AE), URL `javascript:alert(1)`, `ftp://x`, `https://ok.example` | email 400; phone normalized to +971501234567; javascript/ftp 400; https ok | P1 | Sec | Unit |
| CF-014e | Lookup cannot reference another org's record | Lookup → Party | Set the value to an Org B party id; to a deleted or non-allowed-entity record | 400 not found, the same as for a random id (no existence disclosure) | P1 | Sec | API |
| CF-014f | Limits (DECIDED: 50 active fields per record type, 10 searchable; BA CF-LIM) | — | 51st active field on one record type; 11th searchable field; 201 options on a select; text 4,001 chars; multi select 51 values | Each → 400/409 | P2 | Neg | API |
| CF-015 | Option ordering | options with sortOrder 3,1,2 | GET definition; render the dropdown | Options returned and rendered by sortOrder; reorder via PUT is persisted; existing values are unaffected | P2 | Func | API + Comp |
| CF-016 | Deactivated or deleted option on existing records (BA CF-LIF-03) | 10 records hold "bronze" | Deactivate option "bronze"; GET the records; edit a record without touching the field; try to set "bronze" on a new record; try to DELETE the option | Existing values preserved and shown as "bronze (inactive)"; saving an untouched record keeps it **[ASSUMPTION]**; new writes → 400; deleting an in-use option → 409; option key immutable, label editable | P1 | Func | API + Comp |
| CF-017 | Type change is blocked except lossless widenings (BA CF-TYP-01/02) | Fields with data | PUT type: Integer → Text; Date → Text; Text → Long text; Integer → Decimal; Single → Multi select | First two → 409; the three widenings → 200, values intact, version bumped, audited | P2 | Neg | API |
| CF-018 | Key format and immutability (BA CF-NAM-01/02) | — | Create keys "Floors", "cf_1st" (accepted if allowed), "cf_a b", "cf_x'--", "floors" (no prefix), 60 chars, "cf_valid_key"; PUT a key change after save | Only lower-snake `cf_` keys are accepted; a key change after save → 400 | P1 | Sec | Unit + API |
| CF-019 | Duplicate key per entity type in one org | `cf_floors` on Unit exists | Create `cf_floors` on Unit again; create `cf_floors` on Customer | 409; ok (unique per OrgId + EntityType + Key) | P1 | Neg | API |

### 6b. Required vs optional; create vs update

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| CF-020 | Required on create | `cf_ref_code` required | POST the entity without `cf_ref_code`; with null; with "" | 400 `errors["customFields.cf_ref_code"]` each time | P1 | Neg | API |
| CF-021 | Required on update (full PUT) | — | PUT the entity omitting `customFields.cf_ref_code` | 400 (or keep the existing value if PUT merges; **define the semantics; test asserts merge = keep, explicit null = 400**) | P1 | Neg | API |
| CF-022 | Making a field required with existing records (BA CF-VAL-03) | 100 records without a value | Set required | Admin sees the affected count before saving; records stay viewable; the next user edit requires the value | P1 | Func | API |
| CF-022a | Required fields never block system operations (BA CF-VAL-05) | Required custom field on Invoice | Run the billing run job, auto-posting, nightly jobs | Invoices are created and posted without the value; enforcement only on user create/edit and import | P1 | Func | API |
| CF-022b | Default value on create only (BA CF-VAL-02) | Default "today" | Create a record without the value; check old records | New record gets today's date (org TZ); old records are not back-filled | P2 | Func | API |
| CF-023 | Optional field omitted | — | POST without the optional field | 201; the key is absent or null in the JSON | P2 | Func | API |
| CF-023a | Unique validator (BA CF-VAL-04) | `cf_ref_code` unique per org | Save the same value on 2 records; add unique while duplicates exist | 2nd → 409; adding unique → blocked with a list of duplicates; uniqueness per org (Org B can reuse the value) | P2 | Func | API |
| CF-024 | Required + deactivated | Required field deactivated | POST without it | 201 (inactive fields are not required) | P2 | Func | API |

### 6c. Organization isolation

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| CF-030 | Org B cannot see Org A definitions | A defines `cf_vip_tier` on Customer | As B: GET `/custom-fields?entityType=Customer`; GET `/custom-fields/{A-id}` | Not listed; 404 | P1 | Sec | API |
| CF-031 | Org B cannot use Org A's key | A has `cf_vip_tier`; B has none | As B: POST a customer with `customFields.cf_vip_tier="gold"` | 400 unknown key (not silently stored) | P1 | Sec | API |
| CF-032 | Org B cannot modify Org A's definition | — | As B: PUT/deactivate `{A-id}` | 404; the A definition is unchanged | P1 | Sec | API |
| CF-033 | Same key in two orgs, different configs | A `cf_tier` select [gold,silver]; B `cf_tier` number 1–5 | Save "gold" in A and 3 in B; validate cross values | Each validated against its own org's definition; no collision in the unique index (includes OrgId) or in the cache (definition cache keyed by OrgId + EntityType) | P1 | Sec | API |
| CF-034 | Definition cache is per org | Warm A's definition cache | B creates `cf_tier`; A reads | A's definitions are unaffected; B sees its own immediately | P1 | Sec | API |
| CF-034a | Entity-restricted definition (BA CF-SEC-04) | Field applies to ENT-0002 only | Open a Harbour (ENT-0001) record; POST a value on an ENT-0001 record | Hidden and not validated on ENT-0001; a write there → 400; stored values are kept | P2 | Func | API |
| CF-035 | Values follow the record's entity scope | Restricted user (ENT-0002) | Filter `cf.cf_tier=gold` | Only ENT-0002 records (the entity filter still applies) | P1 | Sec | API |

### 6d. Deactivate and reactivate

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| CF-040 | Deactivate hides the field but keeps the data | 20 records have `cf_ref_code` | Deactivate; open a record form; GET the record; check the DB JSON | Hidden from forms, list columns and filters; DB JSON still has the value; the API omits it by default (A-CF2) | P1 | Func | API + E2E |
| CF-041 | Saving a record does not wipe the inactive value | — | Edit another field on the record and save | `cf_ref_code` is still in the JSON (the merge preserves unknown or inactive keys server-side) | P1 | Func | API |
| CF-042 | Writes to an inactive field are rejected | — | PUT with `customFields.cf_ref_code` | 400 "field inactive" | P2 | Neg | API |
| CF-043 | Admin can read inactive values | Org admin | GET `?includeInactiveFields=true` | Inactive values included and flagged `inactive: true`; a non-admin gets 403 for this flag **[ASSUMPTION]** | P2 | Sec | API |
| CF-044 | Export behaviour | — | `/customers/export.csv` before and after deactivation | Active: a column is present; inactive: no column by default (A-CF2) | P2 | Func | API |
| CF-045 | Reactivate restores it | — | Reactivate | Field visible again with the original values; no data loss; audit events for both toggles | P1 | Func | API + E2E |
| CF-045a | Delete only after deactivation; key reserved (BA CF-LIF-02, CF-NAM-03) | — | DELETE an active field; deactivate → DELETE with a wrong confirm key; correct key; create a new field with the same key within 90 days | 409; 400; 200 (values soft-deleted, purged after 30 days, audited); same key → 409 reserved | P2 | Func | API |
| CF-045b | Pack fields cannot be deleted or retyped (BA CF-PCK-02) | `cf_re_view` from the RE pack | DELETE; PUT type; relabel | 409; 409; relabel ok | P2 | Func | API |
| CF-046 | Filter on an inactive field | — | `?cf.cf_ref_code=X` while inactive | 400 "unknown or inactive field" | P2 | Neg | API |

### 6e. Search and filter

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| CF-050 | Filter on a searchable text field | `cf_ref_code` searchable | `?cf.cf_ref_code=ABC-1234` and `?cf.cf_ref_code[contains]=ABC` | Exact and contains matches; translates to `JSON_VALUE(CustomFields,'$.cf_ref_code')` with a **parameterized** value | P1 | Func | API |
| CF-051 | Range filter on number, decimal and date | Searchable `cf_floors`, `cf_handover_date` | `cf.cf_floors[gte]=10&cf.cf_floors[lt]=20`; date range | Correct results; values compared numerically (CAST), not as strings ("9" < "10") | P1 | Func | API |
| CF-052 | Filter on a select and multi select | — | `cf.cf_tier=gold`; `cf.cf_amenities[has]=pool` | Multi select uses `OPENJSON`/`EXISTS`; correct results | P2 | Func | API |
| CF-053 | Filter on a non-searchable field is rejected | `cf_notes_x` not searchable | `?cf.cf_notes_x=a` | 400 "field not searchable" | P1 | Neg | API |
| CF-054 | Sort on a custom field | — | `sort=cf.cf_floors desc` | Allowed only for searchable fields; nulls last | P3 | Func | API |
| CF-055 | Tenant scoping on JSON queries | A and B both have `cf_tier`="gold" records | As A, `cf.cf_tier=gold` | Only A rows (the generated SQL keeps the OrgId and entity predicates; verify with captured SQL in the test) | P1 | Sec | API |
| CF-056 | Global search includes searchable custom text | — | `/search?q=ABC-1234` | Matches records by custom field; scoped by org, entity and module (§4.28) | P3 | Func | API |
| CF-057 | Index or performance on a filterable field | 100k units per org, 2 orgs, 50 fields, 10 filterable (BA NFR-01) | k6: filter `cf.cf_ref_code=…` p95 | < 500 ms p95 (§13.2) via `CustomFieldIndex (OrgId, FieldId, Value*)`; execution plan = index seek with the OrgId predicate; also record the baseline of a raw `JSON_VALUE` scan | P2 | Perf | Perf (k6) + API |
| CF-058 | Index table stays consistent with the JSON | — | Create, update, clear and delete records; mark a field filterable on existing data (backfill); roll back a failed save | `CustomFieldIndex` rows equal the JSON values after every operation (same transaction); a rollback leaves no orphan rows; no runtime DDL and no core migration (CORE-024) | P1 | Func | API |

### 6f. API contract and UI rendering

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| CF-060 | DTO contract | — | OpenAPI snapshot of Customer DTO and `/custom-fields` | `customFields: object<string, string\|number\|boolean\|string[]\|null>`; definitions schema stable; errors keyed `customFields.<key>` | P1 | Contract | API |
| CF-061 | Validation error shape | — | POST with 3 invalid custom values | 400 ProblemDetails with 3 field errors mapped to the form (§10.2) | P1 | Func | API + Comp |
| CF-062 | Dynamic ng-zorro controls per type | Definitions of every type | Render the form | text → `nz-input`; long text → `textarea nz-input`; number/decimal → `nz-input-number` (precision honoured); date → `nz-date-picker` (date only); boolean → `nz-switch` or `label nz-checkbox`; single → `nz-select`; multi → `nz-select nzMode="multiple"`; required marker; sortOrder respected; `data-testid="cf-<key>"` | P1 | Func | Comp |
| CF-063 | Client-side validation mirrors the server | — | Enter invalid values per type | Inline errors before submit; the server still validates (CF-001…) | P2 | Func | Comp |
| CF-064 | Read-only display | Viewer role | Open the detail page | Values formatted per type (MoneyPipe-style decimals, dd MMM yyyy dates, Yes/No, tags for multi); no inputs | P2 | Func | Comp + E2E |
| CF-065 | List columns | — | Add a custom column to the list; CSV export | Column shown, sortable or filterable only if searchable; included in the CSV (§9 "usable in exports") | P2 | Func | E2E |
| CF-066 | Accessibility of dynamic controls | — | axe on the form with all types | Labels associated; errors announced | P3 | A11y | E2E |

### 6g. Security

| ID | Title | Preconditions | Steps | Expected result | Pri | Type | Automation |
|---|---|---|---|---|---|---|---|
| CF-070 | SQL injection via the field key in a filter | — | `?cf.a')%20OR%201=1--=x`, `?cf.cf_ref_code'];DROP TABLE x;--=1`, key with `$`, `.`, `[`, `"` | 400 (the key must match the regex **and** exist in the org's searchable definitions before any SQL is built); the JSON path is built from the allow-listed key only, never concatenated from input; no 500; no extra rows | P1 | Sec | API |
| CF-071 | SQL injection via filter values | — | `cf.cf_ref_code=' OR '1'='1`, `cf.cf_floors[gte]=1;WAITFOR DELAY '0:0:5'` | Treated as literal parameters; 0 or correct rows; response time < 1 s; numeric parse error → 400 | P1 | Sec | API |
| CF-072 | SQL injection via the definition key or label | — | Create a definition with key `x;--` (rejected) and label `'); DROP TABLE Customers;--` | The key is rejected by the regex; the label is stored literally (parameterized) | P1 | Sec | API |
| CF-073 | JSON path injection | — | Key `a.b`, `a[0]`, `$..*` via any input reaching `JSON_VALUE` | Rejected; the path is always `'$."<validated key>"'` built server-side | P1 | Sec | Unit + API |
| CF-074 | Stored XSS in the label | — | Label `<img src=x onerror=alert(1)>` | Rendered as text in the form, list header, filter UI and CSV (CSV formula injection: a value starting with `= + - @` is escaped with `'`) | P1 | Sec | E2E + Comp |
| CF-075 | Stored XSS in values and option labels | — | Value `<script>alert(1)</script>`; option label with `javascript:` | Rendered as text; no `innerHTML` | P1 | Sec | E2E |
| CF-076 | Oversized payload | Limit 16 KB per record (A-CF1) | PUT with a 17 KB `customFields`; a 10 MB body | 400 / 413 (Kestrel body limit); no partial write | P1 | Sec | API |
| CF-077 | Deeply nested JSON | — | `customFields.x = {"a":{"b":{"c":…}}}` 100 levels deep; arrays of objects | 400 (values must be scalars or arrays of scalars; `MaxDepth` set on the serializer); no stack overflow | P1 | Sec | API |
| CF-078 | Too many fields | 50 active definitions (limit) | Create the 51st; send 500 keys in a payload | 409 limit reached; 400 too many / unknown keys | P2 | Neg | API |
| CF-079 | Unknown keys | — | POST with `customFields.not_defined=1` | 400 unknown key (not silently stored, prevents a hidden data channel) | P1 | Sec | API |
| CF-080 | Type confusion | — | Number field gets `{"$gt":0}`, `[1]`, `true`; boolean gets `"false"`; date gets `{}` | 400 each; no coercion | P1 | Sec | API |
| CF-081 | Mass assignment | — | POST an entity with `customFields` plus top-level `orgId`, `id`, `createdBy`, `isDeleted`; PUT a definition with `orgId`, `entityType` change | Only allowed properties bound; `orgId` etc. ignored (TI-004); entityType change → 400 | P1 | Sec | API |
| CF-082 | Duplicate JSON keys | — | Raw body `{"customFields":{"cf_tier":"gold","cf_tier":"admin"}}` | 400 (the serializer rejects duplicate properties) or deterministic last-wins with validation; documented | P2 | Sec | API |
| CF-083 | Definition management needs `core.customfields.manage` (BA CF-SEC-01/02/05, mapped to the key format) | `qa.finance` (has `core.customfields.read` only) | GET `/custom-fields?recordType=Unit`; POST a definition; Org admin adds a Lease field while `realestate` is disabled | 200; 403; 403/409 (needs `realestate.leases.read` and an enabled module) | P1 | Sec | API |
| CF-083a | Per-role visibility and edit rights enforced server-side (BA CF-PRM-01/03) | Field visible to Finance manager only | GET the record as `qa.leasing`; PUT the value as `qa.leasing` | Field absent from the DTO, list, export and filter (filter → 400); write → 403 | P1 | Sec | API |
| CF-084 | Value writes need the owner's write permission | `qa.viewer` | PUT a customer's custom fields only | 403 | P1 | Sec | API |
| CF-085 | Sensitive custom field masking (BA CF-PRM-02) | Field flagged sensitive | Read as a user without permission via the UI, API, CSV export and audit diff | Masked (`••••`) on every surface | P2 | Sec | API + E2E |
| CF-086 | DB constraint backstop | — | Bypass the API and write invalid JSON directly | `ISJSON` CHECK rejects it (Testcontainers) | P2 | Sec | API |
| CF-087 | Audit of custom field changes | — | Change a custom value | AuditEvent before/after JSON includes the custom field diff (§7.3) | P2 | Func | API |

---

## 7. Traceability summary

| Area | v0.1 cases | v0.3 cases | v0.4 cases | v0.4 P1 | Spec / decision refs |
|---|---|---|---|---|---|
| Tenant isolation (TI) | 25 | 25 | 25 | 20 | §7.3, §10.3, §10.6, §10.7 |
| Entity scoping (ES) | 10 | 10 | 10 | 9 | §2.2, §4.30, §7.3 |
| Permissions (PERM) | 28 + 207 matrix | 40 + 432 matrix | 43 + 450 matrix | 35 + matrix | §7.1–7.3 + D-PERM, D-SENS, D-SAL |
| Modules (MOD) | 20 | 20 | 20 | 11 | A-M1…A-M10 |
| Login, refresh, logout, token (AUTH) | 50 | 70 | 70 | 50 | §4.1, §7.3, §10.4 + D-TOK, D-KMSI |
| Workspaces (WS) | — | 17 | 17 | 12 | D-WS |
| Password reset (PWR) | — | 14 | 16 | 10 | D-PWR |
| Password policy (PWP) | — | 22 | 25 | 20 | D-PWP |
| Industry-agnostic core (CORE) | 26 | 33 | 34 | 29 | §1a + D-TPL, D-FIN |
| Test-only seeding endpoint (TS), new | — | — | 3 | 2 | D-TSE |
| Cheques as core finance (CHQ) | — | 12 | 20 | 17 | D-CHQ |
| Custom fields (CF) | 84 | 84 | 84 | 50 | §9, BA §8, D-CFL |
| **Total** | **243 + 207** | **347 + 432** | **367 + 450** | **265** | |

---

## 8. Mismatches between the UX docs (`/workspace/erp-team/uiux/`) and the decisions

The tests follow the decisions; the UX docs need these updates (or the decision needs revisiting).

| ID | UX doc | UX says | Decision / test expectation | Impact |
|---|---|---|---|---|
| G-UX-1 | 01-login (Keep me signed in) | Tooltip "Keeps you signed in on this device for 14 days" | 30 days persistent; session-only when unticked (D-KMSI) | Copy change; AUTH-076 |
| G-UX-2 | 01-login (states) | "Your account is locked or inactive. Contact your workspace administrator." | Generic lockout message **with retry time**; no account-state disclosure (A-A1, AUTH-008/011/012a) | Copy and behaviour change |
| G-UX-3 | 01-login (Workspace field) | A workspace field/radio list on the login card when the email belongs to several workspaces | Picker only **after** a successful password check, listing only the user's memberships (WS-002/011); showing it earlier would enumerate memberships | Flow change |
| G-UX-4 | 07-app-shell | "No workspace switcher in v1 (one workspace per login)" | Users can belong to several workspaces and switching issues new scoped tokens (D-WS, WS-006); needs a switch entry point (user menu or "Change workspace") | New UI element |
| G-UX-5 | 02-password-recovery | Reset link "expires in 60 min" | 30 minutes, single-use (D-PWR) | Copy change |
| G-UX-6 | 02-password-recovery | Policy adds "not equal to the email"; "Sign me out of other devices" can be unticked | **Resolved**: not-equal-to-email (case-insensitive) is DECIDED and matches UX; reset always signs out other devices, so the UX checkbox must be removed (PWR-009b) | UX to remove the checkbox |
| G-UX-7 | 04-users-roles-permissions, 00, 08, 05 | Keys `admin.users`, `admin.settings`, `admin.audit`, `approvals.decide`; example `finance.journal.create` | `<module>.<resource>.<action>` with the §14b catalogue (`core.users.write`, `core.approvals.decide`, `finance.journals.write`); the action vocabulary (create vs write, singular vs plural resource) must match | Role editor and directives |
| G-UX-8 | 08-navigation-ia §3 | Sales and Purchases are core | **Resolved: UX was right** (DECIDED final pass) | None |
| G-UX-9 | 08-navigation-ia §3, 09 | "Leasing (Leases, PDC register)" is in the RE module | Cheques/PDCs are core finance; RE only links them (D-CHQ) | Move the PDC register to Finance |
| G-UX-10 | 09, 08 | UI routes `/real-estate/*` | **Resolved**: key `realestate`, UI `/real-estate/`, API `/m/realestate/`, DB schema `re`, permission prefix `realestate.` | None |
| G-UX-11 | 03-onboarding-wizard | Workspace provisioning out of scope (Q2) | **Resolved**: test-only seeding endpoint in the Testing environment (TS-001/002) | Backend to build |
| G-UX-12 | custom-fields.md (Q8) | Limits 100 fields per resource, 5 searchable | **DECIDED** 50 per record type, 10 searchable | UX copy to update |
| G-UX-13 | 08-navigation-ia §4 | Nav permissions use `.view` (e.g. `finance.invoices.view`) | Key format action is `read` (`finance.invoices.read`) | UX to rename; tests use `.read` |
| G-UX-13 | All UX docs | No `data-testid`s defined | `playwright/README.md` contract | Angular team to adopt |
