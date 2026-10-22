# Coding conventions and review checklist (backend + frontend)

Owner: Full Stack Developer Expert · Applies from sprint 1 · Sources: spec §7 (security), §10 (architecture), §11 (DB), §12 (API),
and the sprint-0 scaffold at `/workspace/erp` (names below match it: `ERP.*`, `OrgEntity`, `IOrgScoped`, `ILegalEntityScoped`,
`IModule`, permission keys `<module>.<resource>.<action>`). Coordinator decisions of 22 Oct 2026 are recorded in §12.

**Must** = blocks merge. **Should** = reviewer may accept with a reason in the PR.

---

## 1. Solution and module layout

```
backend/
  src/ERP.Domain/            core entities, value objects, enums, domain events (no EF, no MediatR)
  src/ERP.Application/       CQRS features, abstractions, behaviours (depends on Domain; EF Core abstractions allowed via IAppDbContext)
  src/ERP.Infrastructure/    EF Core DbContext/configurations/migrations, Identity/JWT, jobs, files, cache, email
  src/ERP.Modularity/        IModule, ModuleRegistry, menu/permission/setting definitions
  src/ERP.Api/               host: Program.cs, middleware, core endpoints, OpenAPI
  modules/ERP.Modules.<Name>/        one project per industry/horizontal module
      Domain/<Feature>/              entities, enums, rules
      Application/<Feature>/         Commands, Queries, Dtos, Validators, Mapper   (one folder per feature)
      Infrastructure/<Feature>/      EF configurations (schema per module, e.g. "re"), integrations
      Api/<Feature>/                 endpoint mapping (IModule.MapEndpoints) or controllers
      <Name>Module.cs                IModule: manifest, permissions, menu, settings, customizable entities, DI, model, endpoints
  tests/ERP.UnitTests · ERP.Application.Tests · ERP.Api.IntegrationTests (Testcontainers SQL Server)
frontend/src/app/
  core/        singletons: api (typed services, models), http (interceptors, problem details), session/auth, custom-fields, guards
  shared/      dumb/reusable UI: crud kit, dynamic fields, page header, status tag, money pipe, entity picker
  features/<module>/<feature>/   *.routes.ts (lazy, default export) · *.page.ts · *.api.ts · *.models.ts · *.store.ts · components/
  layout/      shell (sidebar, header, entity switcher)
```

Rules
- **Must**: a module never references another module's project. Cross-module needs go through `ERP.Application` abstractions or integration events.
- **Must**: core never references module assemblies; the host composes modules (`IModule`).
- **Must**: Domain has no dependency on EF Core, ASP.NET or MediatR.
- **Should**: one feature folder per aggregate; files named after the type they contain.
- **Must**: module DB objects live in the module's schema (`re.`, `inv.`…); core in `core.`/`dbo.` per scaffold.

## 2. CQRS naming (MediatR)

| Kind | Name | Returns | Notes |
|---|---|---|---|
| Command | `CreateUnitTypeCommand`, `UpdateUnitTypeCommand`, `DeleteUnitTypeCommand`, `ActivateLeaseCommand` | DTO or `Unit` | verb + noun; state transitions are separate commands (`/leases/{id}/activate`), never a status field on PUT |
| Query | `GetUnitTypeByIdQuery`, `GetUnitTypesQuery` (paged list), `GetRentRollQuery` | DTO / `PagedResult<T>` | queries are `AsNoTracking()` and project with `Select` — never return entities |
| Handler | `<Request>Handler` | — | one public class per request, same file as request is OK |
| Validator | `<Request>Validator : AbstractValidator<<Request>>` | — | FluentValidation; DTO rules in `<Dto>Validator`, composed with `SetValidator` |
| DTO | `UnitTypeDto` (read), `UnitTypeUpsertDto` (POST/PUT body) | — | records; no entity types in DTOs |

- **Must**: every request that needs a permission implements `IRequirePermission` (AuthorizationBehavior = defence in depth, spec §10.4) **and** the endpoint has `RequirePermission(...)`.
- **Must**: pipeline order Authorization → Validation → CustomFieldValidation → (Transaction for multi-aggregate commands) → handler.
- **Must**: handlers don't call other handlers via `ISender`; share logic through domain methods or application services.
- **Must**: number sequences (`INumberingService`) are taken inside the posting transaction (spec §11.4).
- **Should**: master data uses the generic kit (`starter-code/`); deviate only when the entity has real behaviour.

## 3. API style

- **Must** base path `/api/v1` (URL segment). Core endpoints flat: `/api/v1/<resource>` (`/api/v1/users`, `/api/v1/custom-fields`); module endpoints: `/api/v1/m/{moduleKey}/<resource>` (`/api/v1/m/realestate/unit-types`), mapped in `IModule.MapEndpoints` (or controllers with `[RequireModule(key)]`) and gated by `ModuleEnabledFilter` → 403 `type: …/module-disabled`, data kept. `Asp.Versioning` with `ApiVersion(1.0)`, `ReportApiVersions = true`; a breaking change ⇒ `v2` route kept side by side for one release.
- **Must** JSON camelCase; enums as camelCase strings (`JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`); dates `yyyy-MM-dd`; timestamps UTC ISO-8601 with `Z`; money `decimal(18,2)` serialized as JSON numbers.
- **Must** resources are plural kebab-case nouns (`/unit-types`, `/payroll-runs`); actions are `POST /{id}/<verb>` (spec §12).
- **Must** status codes: 200 GET/PUT, 201 + `Location` POST, 204 DELETE/actions without body, 400 validation, 401, 403 (permission or module disabled — `type: .../module-disabled`), 404, 409 business conflict/duplicate, 412 stale `If-Match`, 422 not used.
- **Must** updates/deletes accept `If-Match: "<base64 rowversion>"`; GET by id / PUT / POST return `ETag`.
- **Must** lists: `?page=1&pageSize=25&sort=name,-createdAt&q=text&filter=field:op:value` (repeatable `filter`; ops `eq ne gt gte lt lte contains startsWith in isNull isNotNull`; `in` values `a|b`; custom fields addressed by their `cf_` key). Response `PagedResult<T> { items, total, page, pageSize, totalPages }`. `pageSize` max 200. Sort/filter fields are an explicit whitelist (`QueryFields<T>`); unknown field ⇒ 400. Default sort is deterministic (add `Id`/`Code` tiebreaker).
- **Should** every list exposes `/export.csv` with the same query params (streamed).
- **Must** OpenAPI: each endpoint has a summary, tags per module, `ProducesProblem` for 400/403/404/409/412.

## 4. Error format (RFC 7807)

All errors are `application/problem+json` produced in **one** place (`IExceptionHandler`):
```json
{ "type": "https://errors.erp.app/validation", "title": "Validation failed", "status": 400,
  "detail": "One or more validation errors occurred.", "instance": "/api/v1/unit-types",
  "code": "validation", "traceId": "00-4bf9…-01",
  "errors": { "code": ["Code may contain letters, digits, '-' and '_' only."], "customFields.cf_floor_count": ["Must be ≥ 1."] } }
```
- **Must** `errors` keys are camelCase JSON paths of the request body (`lines[2].amount`, `customFields.cf_x`) so the UI maps them to controls.
- **Must** stable `code` per failure kind (`validation`, `not_found`, `duplicate_code`, `concurrency`, `forbidden`, `module_disabled`, `period_locked`…).
- **Must** never leak exception messages/stack traces for 500s; log with the `traceId`.
- **Must** domain/application code throws typed exceptions (`FieldValidationException`, `NotFoundException`, `ConflictException`, `ForbiddenException`, `ConcurrencyException`) — controllers never build error responses by hand.

## 5. Tenancy, legal-entity scoping, security

- **Must** every business table has `OrgId` (`IOrgScoped`); entity-scoped tables also `LegalEntityId` (`ILegalEntityScoped`) — spec §11.1.
- **Must** OrgId comes from the token (`org` claim) via the SaveChanges interceptor; never accept `orgId` in a body or query.
- **Must** EF global filters: `OrgId == current` on all `IOrgScoped`; `AllowedLegalEntityIds.Contains(LegalEntityId)` + optional `X-Entity-Id` narrowing on `ILegalEntityScoped`; `!IsDeleted` on `ISoftDelete`. `IgnoreQueryFilters()` only in reviewed code and must re-apply `OrgId` explicitly.
- **Must** writes to entity-scoped rows validate that `LegalEntityId` is in the caller's allowed set (403 otherwise) — the read filter does not protect inserts.
- **Must** unique indexes include `OrgId` (and `LegalEntityId` where the business key is per entity); soft-deleted rows excluded with a filtered index (`WHERE IsDeleted = 0`) unless the key must never be reused.
- **Must** permission keys `<module>.<resource>.<action>` with a **plural** resource (`finance.cheques.approve`, `core.customfields.manage`, `realestate.unittypes.write`), lower case, no separators inside a segment; declared by the owning module (`IModule.Permissions`, prefix = module key). UI hides what the API refuses (`*hasPermission`, route `canMatch`), but the API is the authority.
- **Must** roles are created from **role templates** that copy permissions once (onboarding / module enable); later template changes never rewrite existing roles.
- **Must** modules are enabled per organization (`OrganizationModule`, composed via `IModule` + `ModuleRegistry`); industry is chosen at onboarding and enables its modules; `core` is always on. Module endpoints are gated by `ModuleEnabledFilter` (403 `module-disabled`) and module routes by `moduleEnabledGuard` — data is kept when a module is disabled.
- **Must** one global identity per email (`AppUser`); workspace access, roles and entity access live on the membership (`OrganizationMember`, `UserRole`, `UserLegalEntityAccess`); never key anything by email per org.
- **Must** no secrets/PII in logs; sensitive fields (IBAN, Emirates ID) masked unless `core.sensitive.read`.

## 6. Persistence and auditing

- **Must** IDs are sequential GUIDs generated by EF on Add (don't `Guid.NewGuid()` in constructors — spec §11.1).
- **Must** `RowVersion` (`rowversion`) on every mutable aggregate; concurrency mapped to 412.
- **Must** audit columns (`CreatedAt/By`, `UpdatedAt/By`, `DeletedAt/By`) stamped by the interceptor (UTC, `IClock`); `AuditEvent` (before/after JSON, changed fields, user, IP) written by the audit interceptor in the same transaction — spec §10.7. Custom-field changes are audited as part of the entity's JSON.
- **Must** soft delete for master data only; documents use status transitions (Void/Cancelled) and posted ledger rows are immutable (reversal only).
- **Must** money `decimal(18,2)`, rates `decimal(9,4)`, rounding `MidpointRounding.AwayFromZero` (spec §10.5).
- **Must** one migration per PR touching the model, named `YYYYMMDD_<Module>_<Change>`; reviewed SQL (`dotnet ef migrations script`) attached for destructive changes.
- **Should** read-heavy lists use projections + covering indexes; no `Include` chains in list queries.

## 7. Custom fields (metadata-driven, no redeploy)

- **Must** an extensible entity implements `IHasCustomFields` (`string? CustomFields` → `nvarchar(max)` column `CustomFields` with `CHECK (ISJSON(CustomFields)=1)`), is registered as a customizable entity type by its module (key `"<module>.<entity-kebab>"`, e.g. `realestate.unit-type`; core ones `party`, `item`… per scaffold), and exposes `customFields` on its read DTO and upsert DTO.
- **Must** custom values travel **inside** the entity's own Create/Update command (`ICustomFieldsCommand`) — validated by `CustomFieldValidationBehavior` against the cached **active** definitions; handlers persist the normalized payload, nothing else writes the column.
- **Must** keys: `^cf_[a-z][a-z0-9_]{0,59}$`, unique per org + entity type including deleted definitions; `FieldKey` and `DataType` are immutable; delete = soft delete (values kept, hidden); option values are immutable once used (deactivate instead).
- **Must** canonical storage formats (text/select/lookup string, number JSON number, date `yyyy-MM-dd`, dateTime UTC ISO `Z`, bool, multiSelect string[]).
- **Must** PUT replaces all **active** custom values; values of inactive/deleted fields are preserved by the handler.
- **Must** filter/sort only on definitions flagged `IsFilterable` / `IsSortable`; translation via `JsonFunctions.JsonValue/JsonQuery` (no raw SQL, no string-concatenated values).
- **Must** definition changes invalidate the provider cache (`ICustomFieldDefinitionProvider.Invalidate`) in the same handler; scaled-out deployments publish invalidation over Redis.
- **Must** the UI never hard-codes a custom field: forms/lists/filters come from `GET /custom-fields/{entityType}/metadata`.
- **Must** definitions management requires `core.customfields.manage`; metadata requires the entity type's read permission.
- **Should** promote a hot filterable field to a persisted computed column + index (DBA script, no app change) when a tenant's list query exceeds the SLO (p95 > 500 ms).
- **Must not** use custom fields for data the core logic depends on (posting rules, numbering, approvals). If logic needs it, it becomes a real column.
- Limits: ≤ 100 active fields per entity type, ≤ 50 options per select, payload ≤ 16 KB.

## 8. Frontend (Angular 19, ng-zorro-antd 19.3.1)

- **Must** standalone components (Angular 19 default — do **not** write `standalone: true`; `standalone: false` is a review flag), `ChangeDetectionStrategy.OnPush` by default, control flow (`@if/@for/@switch`), `input()/output()/model()` signal APIs, `inject()` over constructor params, `provideEnvironmentInitializer`/`provideAppInitializer` instead of the deprecated `ENVIRONMENT_INITIALIZER`/`APP_INITIALIZER` tokens.
- **Must** lazy routes per feature (`*.routes.ts` default export, `loadComponent`/`loadChildren`), guarded with `canMatch: [permissionGuard(key), moduleEnabledGuard(moduleKey)]`.
- **Must** HTTP only through typed API services in `features/**/*.api.ts` or `core/api` (extend `CrudApiService` for master data). Components never inject `HttpClient`.
- **Must** interceptors (functional `HttpInterceptorFn`): bearer token, 401 → single-flight silent refresh, `X-Entity-Id` from the session store, global error toast for 5xx/0 (not for 400/409/412, which the form handles).
- **Must** state: signals. Global stores (session: user, permissions, entity scope, theme; enabled modules/menu) in `core/`; per-feature list/filter state in a signal store (`CrudListStore` for CRUD, `@ngrx/signals` SignalStore for richer screens). No `BehaviorSubject` stores in new code; RxJS for async streams (search debounce, `switchMap` cancellation).
- **Must** typed reactive forms; server errors mapped to controls with `applyProblemDetailsToForm` (keys = ProblemDetails `errors` paths); 412 shows "record changed, reload".
- **Must** lists use `nz-table` with `nzFrontPagination=false` and `(nzQueryParams)` → server paging/sort; page size 25; toolbar search + filters + refresh + CSV; footer "N records".
- **Must** no business rules only in the UI (validation is duplicated for UX, the API is authoritative).
- **Should** shared building blocks from spec §10.2 (`PageHeader`, `StatusTag`, `MoneyPipe`, `EntityPicker`…) instead of ad-hoc markup; theme via CSS variables; RTL-safe styles (logical properties).
- **Must** accessibility: every control has a label (`nzFor`), icon-only buttons have `title`/`aria-label`.
- **Must** strict TypeScript (`strict`, `strictTemplates`), no `any` in public APIs. CI runs `ng build` (or `ngc -p` with `strictTemplates`).

## 9. Testing expectations

| Layer | What | Tool | Bar |
|---|---|---|---|
| Domain | invariants, state machines, calculations | xUnit + FluentAssertions | every rule / transition |
| Application | handlers incl. tenancy + permission + validation failures; `CustomFieldValueValidator` table tests per data type | xUnit, EF InMemory **not** allowed for query tests (use SQL) | happy path + each failure code |
| API integration | endpoints end-to-end against SQL Server (Testcontainers): 401/403/404/409/412, cross-org isolation, entity scoping, custom-field filter SQL | WebApplicationFactory + Testcontainers | each new endpoint |
| Frontend unit | stores, mappers, form builders (`toApiValues`, `toCriteria`), guards | Jest/Karma + Angular TestBed | logic files ≥ 80 % lines |
| Performance (from sprint 2) | list endpoints with custom filters (10k rows), token size with max permissions, `/me` latency | k6/NBomber + SQL plans | p95 < 500 ms |
| E2E | critical flows (login, entity switch, create master data, add custom field → visible) | Playwright | per sprint demo flow |
- **Must** a tenant-isolation test for every new aggregate (org A cannot read/update org B by id).
- **Must** CI green: build, tests, `dotnet format --verify-no-changes`, `ng lint`, `ng build --configuration production`.

## 10. Git / PR hygiene

- Branch `feat/<module>-<short>`; Conventional Commits; PR ≤ ~400 changed lines excluding generated migrations; screenshots for UI.
- PR description: what/why, spec section, permission keys added, migration name, test evidence.

## 11. PR review checklist (copy into the PR template)

**Architecture**
- [ ] Code sits in the right layer/module; no module→module reference; Domain free of infrastructure.
- [ ] Request/handler/validator names follow §2; queries project to DTOs with `AsNoTracking`.

**Security & tenancy**
- [ ] Endpoint `RequirePermission(...)` **and** request `IRequirePermission`; new keys declared in the module and seeded to roles.
- [ ] No `OrgId` from client; `IgnoreQueryFilters` justified and re-filters org; entity-scoped writes check the allowed set.
- [ ] Module endpoints under `/api/v1/m/{key}` gated by `ModuleEnabledFilter`/`[RequireModule]`; permission keys `<module>.<plural-resource>.<action>`; no PII in logs.

**API contract**
- [ ] `/api/v1`, plural kebab resources, correct status codes, `ETag`/`If-Match` on mutable resources.
- [ ] Lists follow page/sort/q/filter contract with whitelisted fields and deterministic default sort.
- [ ] Errors only via typed exceptions → ProblemDetails (`code`, `traceId`, `errors` keys match body paths).

**Data**
- [ ] Common columns, rowversion, indexes include `OrgId`; money/rate precision; migration reviewed.
- [ ] Audit captured (interceptor) for new aggregates.
- [ ] Custom fields (if extensible): `IHasCustomFields`, registered entity type, `customFields` on DTOs, command implements `ICustomFieldsCommand`, projection includes the JSON column.

**Frontend**
- [ ] Standalone (no explicit `standalone: true`) + OnPush + signals; lazy route with `permissionGuard`/`moduleEnabledGuard`; typed API service.
- [ ] ProblemDetails mapped to fields; 412 handled; loading/empty/error states; labels/aria.
- [ ] No hard-coded custom fields; list uses server paging/sort.

**Quality**
- [ ] Tests per §9 incl. tenant isolation; CI green; OpenAPI updated; README/docs updated when a pattern changes.

## 12. Decisions (coordinator, 22 Oct 2026) — no longer open

| Topic | Spec | Decision |
|---|---|---|
| Permission keys | `<area>.read/write`, `admin.users` (§7.1) | `<module>.<resource>.<action>`, resource plural (`finance.cheques.approve`, `core.customfields.manage`). Spec §7.2 areas are mapped onto these keys in the role templates. |
| Endpoint paths | flat `/api/v1/units` (§12) | core flat under `/api/v1/…`; modules under `/api/v1/m/{key}/…`; disabled module ⇒ 403 `module-disabled`, data kept. |
| Roles | fixed in demo, custom roles "new" (§9) | sprint 1: role templates copied once into each organization; custom-role editor in sprint 2. |
| Identity | "email unique" | one global identity per email; an email can belong to several workspaces; workspace picker after a correct password. |
| Password recovery | admin reset only (§4.26) vs `/auth/forgot` (§10.4) | both: admin "reset password" + self-service `/auth/forgot` (single-use token, 30 min, newest link invalidates older ones, success clears lockout, neutral response). |
| Modules | not in spec | enabled per organization via `IModule` + `ModuleRegistry`; industry chosen at onboarding; `core` always on. |
| Angular | 18+ (§10.2) | Angular 19 + ng-zorro-antd 19.3.1. |
| Studio permission | none | `core.customfields.manage` / `core.customfields.read`. |

Still open: Unit type as a lookup vs free text (spec §4 units) — the kit's example assumes a lookup.
