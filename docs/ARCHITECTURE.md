# Architecture

A modular, multi-tenant ERP for any business. The **core** is industry- and country-neutral; industries plug in as **modules** (real estate, inventory, retail, HR...) and country rules as **country packs**.

## Solution layout

```
backend/
  src/ERP.Domain          entities, enums, domain rules, list attributes. No dependencies.
  src/ERP.Application     use cases (MediatR commands/queries + handlers), validation, abstractions (IRepository<T>, IEntityStore, IUserDirectory...), paging, ApiResponse
  src/ERP.Infrastructure  EF Core (SQL Server; SQLite in tests), Identity, auth, generic EfRepository<T>, services
  src/ERP.Modularity      IModule contract, registry, module gate, API envelope filter, one-line list endpoints
  src/ERP.Api             composition root, minimal-API endpoints, middleware, SignalR hub
  modules/ERP.Modules.*   pluggable modules (RealEstate, Inventory sample)
  countrypacks/ERP.CountryPacks.*  country rules (tax codes, country-specific custom fields); referenced only by the host
  tests/                  unit (domain, paging, custom fields, architecture rules) and integration (WebApplicationFactory)
frontend/                 Angular 19 + ng-zorro-antd 19.3.1
database/scripts/         idempotent SQL generated from EF migrations
deploy/                   docker-compose for local SQL Server + API
```

Dependency rule: Domain ← Application ← Infrastructure ← Api. Modules reference Application + Modularity only. NetArchTest rules enforce: core never references a module or country pack, modules never reference each other, Domain references nothing.

## Request flow: Endpoint → MediatR (CQRS) → Repository → DB
```
HTTP  →  Endpoint (ERP.Api.Endpoints)        binds the request, sends ONE command/query, shapes the HTTP result
      →  MediatR pipeline                    AuthorizationBehavior (IRequirePermission) → ValidationBehavior (FluentValidation)
      →  Handler (ERP.Application)           business rules; orchestrates repositories and application services
      →  IRepository<T> / IReadRepository<T> generic EfRepository<T> (Infrastructure), tenant / legal-entity / soft-delete filters
      →  DbContext → database
```
- Queries (`IQuery<T>`) read through `IReadRepository<T>`; commands (`ICommand<T>`, `ICommand`) write through `IRepository<T>` / `IUnitOfWork`.
- **Endpoints never touch repositories, services, `DbContext` or EF.** Cookies, headers, files and the client IP are HTTP concerns: the endpoint reads them and passes plain values into the request.
- **Application never sees the `DbContext`.** Infrastructure services that genuinely talk to a store (Identity, JWT/refresh tokens, onboarding) sit behind Application interfaces (`IAuthService`, `IUserDirectory`, `IOnboardingService`, `IEntityStore`).
- Lists need no handler: `MapPagedList<T, TDto>` sends a `ListQuery<T, TDto>` that one open-generic `ListRequestHandler` executes.
- Enforced by `ArchitectureTests`: endpoints depend only on MediatR + request/DTO types, the API host references Infrastructure only in `Program.cs`, Application has no `DbContext`/`DbSet` dependency, module code outside handlers has no repository dependency, and every request type has a handler.

## Country packs
`ICountryPack` (Application) supplies a country's tax codes and country-specific custom fields. A pack is its own project under `backend/countrypacks`, registered only by the host, and applied by onboarding when a workspace is created with that country code; countries without a pack get the neutral core defaults. `ERP.CountryPacks.AE` provides VAT 5%/0%/exempt and the party fields TRN, Emirates ID, trade licence number and licensing authority. Pack fields are ordinary custom fields afterwards (editable, validated, searchable).

## Multi-tenancy and scoping
- Organization (UI: "Workspace") → Legal entities (accounting boundary) → Business units.
- `OrgId` comes from the JWT only; EF global query filters scope every `IOrgScoped` row; writes to another org are rejected in the save interceptor.
- `X-Entity-Id` selects a legal entity; it is validated against the caller's allowed entities. `ILegalEntityScoped` rows are filtered by the allowed set.
- Soft delete (`ISoftDelete`) is a global filter; audit stamps and the audit trail are written by `AuditSaveChangesInterceptor`.
- Keys are sequential GUIDs (`SequentialGuid`) to keep clustered indexes append-only.

## Data access: generic repository
- `IReadRepository<T>` / `IRepository<T>` (Application) with `ISpecification<T>` / `Specification<T>` for criteria and includes; implemented once by `EfRepository<T>` (Infrastructure) and registered as open generics. No per-entity repositories.
- The request's `DbContext` is the unit of work (`IUnitOfWork`, `IRepository<T>.SaveChangesAsync`).
- Handlers depend on abstractions (`IRepository<T>`, `IUnitOfWork`, `IEntityStore` for metadata-driven access by CLR type), never on `AppDbContext`. `IAppDbContext` is internal to Infrastructure.

## Lists: paging, sorting, search, filters (generic)
- Request: `PagedQuery` → `?page=1&pageSize=25&sort=name,-createdAt&search=acme&filter=isActive:eq:true&filter=roles:has:customer`.
  Filter ops: `eq ne gt gte lt lte contains startsWith in (a|b) has (flags) isNull isNotNull`. Page size is clamped to 1..200.
- Whitelist by attribute on the entity: `[Searchable]`, `[Sortable]`, `[Filterable]`, `[Listable]` (= all three), `[DefaultSort("code")]`. Anything not annotated is rejected with 400 (e.g. sensitive fields can never be sorted or filtered).
- `IQueryable` extensions: `ApplySearch`, `ApplyFilters`, `ApplySort` (always ends with `Id` for stable paging), `ApplyPaging`, `ToPagedResultAsync` (with optional server-side projection). Values are sent as SQL parameters.
- A new table gets a full list endpoint in one line:
  ```csharp
  group.MapPagedList<Warehouse, WarehouseDto>("/warehouses", WarehousesRead, w => new WarehouseDto(...));
  // with custom fields:
  group.MapPagedList<Item, object>("", CorePermissions.ItemsRead, "item", (i, cf) => new { i.Id, i.Name, customFields = cf });
  ```
- Frontend: `CrudApiService<TDto, TInput>` (one line per resource), `createPagedList(source)` state, and the `<erp-paged-table>` component (search box, server sort, paging, empty/loading states).

## API conventions
- Versioned under `/api/v1`; module APIs under `/api/v1/m/{moduleKey}/...`. A disabled module returns 403 with type `.../module-disabled`; its data is kept.
- **Response envelope** on every endpoint (applied by `ApiEnvelopeFilter` on the `/api/v1` group; endpoints return plain values / `Results.Ok` / `Results.Created`):
  ```json
  { "success": true, "statusCode": 200, "message": "OK", "data": { }, "traceId": "..." }
  { "success": true, "statusCode": 200, "message": "OK", "data": [ ], "meta": { "page": 1, "pageSize": 25, "total": 3, "totalPages": 1, "hasNext": false, "hasPrevious": false }, "traceId": "..." }
  { "success": false, "statusCode": 400, "message": "Validation failed", "errors": { "name": ["..."] }, "traceId": "...",
    "type": "...", "title": "Validation failed", "status": 400, "detail": null }
  ```
  Errors keep the RFC 7807 members (`type, title, status, detail`, extensions such as `retryAt`) and use `application/problem+json`. Exceptions (`ProblemExceptionHandler`), validation, module gate, legal-entity scope, rate limiting (429) and empty 401/403/404 (status-code pages) all produce the same shape. 204 and file downloads are not wrapped.
- The Angular `envelopeInterceptor` unwraps `data` (paged → `{ items, ...meta }`); `asProblem()` reads error envelopes.

## Auth and permissions
- ASP.NET Identity, login by email, multi-workspace: login returns the workspace list when the user belongs to several; the token is issued for the chosen workspace. Switching workspace revokes the old workspace's tokens.
- Access JWT 5 minutes (in memory in the browser); refresh token rotated on use, HttpOnly/Secure/SameSite=Strict cookie scoped to `/api/v1/auth`, 30 days with "Keep me signed in" else session-only; reuse detection revokes the family; logout denylists the `jti`.
- Lockout after 5 failed attempts for 15 minutes with a generic message; password ≥ 10 chars with upper, lower and digit, not equal to the email; reset tokens single-use, 30 minutes, success signs out other devices.
- Permission keys `<module>.<resource>.<action>`, lowercase, plural resources, `.read` (never `.view`). Policies `perm:<key>`; `*` for workspace owners. Masked IBAN/ID values require `core.sensitive.read`.

## Modules
`IModule` declares manifest (key, category, requires), permissions, menu items, settings, customizable entities, EF model configuration and endpoints. The registry discovers modules; enablement is per workspace (`OrganizationModule`); industry templates (chart of accounts, roles, sequences, settings) are applied at onboarding.

## Finance (core)
Double-entry GL (accounts, journal entries/lines with balanced-entry invariants), tax codes from data (no hard-coded country rules), currencies and exchange rates, invoices/bills/settlements (schema), and bidirectional cheques/payment instruments (`finance.cheques.*`; issuing an outgoing cheque needs no approval, deposit/clear/bounce/replace/cancel go through maker-checker approval). Payroll postings are aggregated per run.

## Customization (custom fields)
Odoo Studio pattern: a metadata registry drives storage, validation and UI.
- `CustomFieldDefinition` (OrgId, EntityType, Key `^cf_[a-z0-9_]{1,60}$`, Label, DataType text/number/decimal/date/dateTime/bool/select/multiSelect/lookup, Required, DefaultValue, Options JSON, Section, Order, HelpText, IsSearchable, ShowInList, Sortable, LegalEntityId scope, IsActive).
- Extensible entities implement `IHasCustomFields` (one `CustomFields` JSON column; `ISJSON` check constraint on SQL Server). Entity types are registered by the core and by modules (`core.party`, `inventory.warehouse`...).
- Values are validated in the Application layer (`CustomFieldValidator`): type, required, length/pattern, range, choices, unknown keys rejected; values are normalized (ISO dates, UTC date-times). Inactive definitions keep their data.
- APIs: definitions CRUD (`core.customfields.manage`), form metadata per entity type, read/write values.
- Indexing searchable fields: on SQL Server add a persisted computed column `JSON_VALUE(CustomFields, '$.cf_key')` with an index per searchable field (created by a background job when `IsSearchable` is turned on); Sprint 1 adds `cf_` keys to the generic filter/sort pipeline.
- Angular: `<erp-dynamic-fields>` / `<erp-dynamic-field>` render sections from metadata in forms; `formatCustomValue` renders list cells.

## Cross-cutting
Serilog request logging, health checks (`/health`), Swagger in development, CORS for the web app, security headers, login rate limiting, SignalR notifications hub.
