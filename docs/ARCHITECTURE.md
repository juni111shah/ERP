# Architecture

A modular, multi-tenant ERP for any business. The **core** is industry- and country-neutral; industries plug in as **modules** (real estate, inventory, retail, HR...) and country rules as **country packs**.

## Solution layout

```
backend/
  src/ERP.Domain          entities, enums, domain rules, list attributes. No dependencies.
  src/ERP.Application     use cases (MediatR commands/queries + handlers), validation, abstractions (IRepository<T>, IEntityStore, IUserDirectory...), paging, ApiResponse
  src/ERP.Infrastructure  EF Core (SQL Server only; tests use an in-memory SQLite harness), Identity, auth, generic EfRepository<T>, services
  src/ERP.Modularity      IModule contract, registry, module/capability gates, ApiControllerBase, API envelope filter
  src/ERP.Api             composition root, API controllers (Controllers/<Area>), middleware, SignalR hub
  modules/ERP.Modules.*   pluggable modules (RealEstate, Inventory sample)
  countrypacks/ERP.CountryPacks.*  country rules (tax codes, country-specific custom fields); referenced only by the host
  tests/                  unit (domain, paging, custom fields, architecture rules) and integration (WebApplicationFactory)
frontend/                 Angular 19 + ng-zorro-antd 19.3.1
database/scripts/         idempotent SQL generated from EF migrations
deploy/                   docker-compose for local SQL Server + API
```

Dependency rule: Domain ← Application ← Infrastructure ← Api. Modules reference Application + Modularity only. NetArchTest rules enforce: core never references a module or country pack, modules never reference each other, Domain references nothing.

## Request flow: Controller → Command/Query → Repository → Database
```
HTTP  →  1. Controller          ERP.Api/Controllers/<Area>/  or  modules/<Module>/Controllers/
                                binds the request, sends ONE command or query, shapes the HTTP result. No rules, no data access.
      →  MediatR pipeline       AuthorizationBehavior (IRequirePermission) → ValidationBehavior (FluentValidation) → concurrency retry
      →  2. Handler (CQRS)      one class per command/query; the business rules, written as numbered steps
      →  3. Repository          I<Entity>Repository: named data methods (GetWithLinesAsync, TotalCreditedAsync...); all LINQ lives here
      →  4. Database            EF Core DbContext (tenant, legal-entity and soft-delete filters always apply) → SQL Server
```

### Where things go (core)
```
src/ERP.Api/Controllers/Finance/InvoicesController.cs           routes of one resource; one small method per route
src/ERP.Application/Finance/Invoices/                           one folder per feature (namespace stays ERP.Application.Finance)
    Commands/CreateInvoice/CreateInvoiceCommand.cs              the request: a record, ICommand<TResult>, its permission
    Commands/CreateInvoice/CreateInvoiceValidator.cs            FluentValidation rules for the request (when it has input)
    Commands/CreateInvoice/CreateInvoiceHandler.cs              ICommandHandler<CreateInvoiceCommand, InvoiceDto>
    Queries/GetInvoiceById/GetInvoiceByIdQuery.cs               IQuery<TResult>
    Queries/GetInvoiceById/GetInvoiceByIdHandler.cs             IQueryHandler<GetInvoiceByIdQuery, InvoiceDto>
    Dtos/                                                       request bodies (InvoiceInput) and responses (InvoiceDto)
    Validators/                                                 validators shared by several commands (InvoiceInputValidator)
    IInvoiceRepository.cs                                       the data methods the handlers need
    InvoiceCreditCaps.cs                                        rules shared by several handlers of the feature (a service, not a base class)
src/ERP.Infrastructure/Repositories/Finance/InvoiceRepository.cs the implementation: RepositoryBase<Invoice> + the named queries
```
Modules use the same shape inside the module project: `Controllers/`, `Features/<Feature>/{Commands,Queries,Dtos}` and `Repositories/` (interface and implementation).

### Rules
- **Names:** a request is `<Verb><Thing>Command` or `<Verb><Thing>Query`; its handler is `<Verb><Thing>Handler` and handles only that request; its validator is `<Verb><Thing>Validator`.
- **Controllers** derive from `ApiControllerBase`, carry `[Route("api/v1/...")]`, one `[Http...]` + `[RequirePermission(...)]` per action (or `[PermissionByRequest(typeof(...))]` when the request decides the permission, `[SignedIn("why")]` for the caller's own things, `[AllowAnonymous]`; a bare `[Authorize]` fails `PermissionCoverageTests`), and return the value, `Created(...)` or `NoContent()`. The `ApiResponse<T>` envelope is added by `ApiEnvelopeResultFilter`. Module controllers add `[ModuleGate("<key>")]` and route under `api/v1/m/<key>/`; optional core areas add `[CapabilityGate("<key>")]`.
- **Controllers never touch repositories, services, `DbContext` or EF.** Cookies, headers, files and the client IP are HTTP concerns: the controller reads them and passes plain values into the request.
- **Handlers** depend on repositories and application services, never on `AppDbContext`. Plain loads and saves can use the generic `IRepository<T>`; any query with more than a simple filter goes into a named method of the entity repository. Rules shared by several handlers go into a small service of the feature.
- **Repositories** derive from `RepositoryBase<T>` (which wraps the generic repository, so every filter still applies) and are registered automatically.
- Lists need no handler: a controller action calls `PagedList(query, SomeDto.Projection, permission, ct)`, which sends a `ListQuery<T, TDto>` that one open-generic `ListRequestHandler` executes.
- Enforced by `ArchitectureTests` (controllers only use MediatR, module controllers are gated, every request is a named command/query with exactly one handler named after it, repositories live in `Repositories`, Application has no `DbContext`) and by `ApiRouteSnapshotTests` (every route, permission, gate and rate limit matches `api-routes.snapshot.txt`, and every route is a controller action).

## Country packs
`ICountryPack` (Application) supplies a country's tax codes and country-specific custom fields. A pack is its own project under `backend/countrypacks`, registered only by the host, and applied by onboarding when a workspace is created with that country code; countries without a pack get the neutral core defaults. `ERP.CountryPacks.AE` provides VAT 5%/0%/exempt and the party fields TRN, Emirates ID, trade licence number and licensing authority. Pack fields are ordinary custom fields afterwards (editable, validated, searchable).

## Multi-tenancy and scoping
- Organization (UI: "Workspace") → Legal entities (accounting boundary) → Business units.
- `OrgId` comes from the JWT only; EF global query filters scope every `IOrgScoped` row; writes to another org are rejected in the save interceptor.
- `X-Entity-Id` selects a legal entity; it is validated against the caller's allowed entities. `ILegalEntityScoped` rows are filtered by the allowed set.
- Soft delete (`ISoftDelete`) is a global filter; audit stamps and the audit trail are written by `AuditSaveChangesInterceptor`.
- Keys are sequential GUIDs (`SequentialGuid`) to keep clustered indexes append-only.

## Data access: repositories
- `IReadRepository<T>` / `IRepository<T>` (Application) with `ISpecification<T>` / `Specification<T>` for criteria and includes; implemented once by `EfRepository<T>` (Infrastructure) and registered as open generics.
- Entity repositories (`IInvoiceRepository : IRepository<Invoice>`) add named queries on top. Their implementations derive from `RepositoryBase<T>`, live in `ERP.Infrastructure/Repositories/<Area>/` (core) or `<Module>/Repositories/` (modules) and are registered by `AddRepositories`.
- The request's `DbContext` is the unit of work (`IUnitOfWork`, `IRepository<T>.SaveChangesAsync`).
- Handlers depend on abstractions (`IRepository<T>`, `IUnitOfWork`, `IEntityStore` for metadata-driven access by CLR type), never on `AppDbContext`. `IAppDbContext` is internal to Infrastructure.

## Lists: paging, sorting, search, filters (generic)
- Request: `PagedQuery` → `?page=1&pageSize=25&sort=name,-createdAt&search=acme&filter=isActive:eq:true&filter=roles:has:customer`.
  Filter ops: `eq ne gt gte lt lte contains startsWith in (a|b) has (flags) isNull isNotNull`. Page size is clamped to 1..200.
- Whitelist by attribute on the entity: `[Searchable]`, `[Sortable]`, `[Filterable]`, `[Listable]` (= all three), `[DefaultSort("code")]`. Anything not annotated is rejected with 400 (e.g. sensitive fields can never be sorted or filtered).
- `IQueryable` extensions: `ApplySearch`, `ApplyFilters`, `ApplySort` (always ends with `Id` for stable paging), `ApplyPaging`, `ToPagedResultAsync` (with optional server-side projection). Values are sent as SQL parameters.
- A new table gets a full list endpoint in one controller action:
  ```csharp
  [HttpGet("warehouses")] [RequirePermission(WarehousesRead)]
  public Task<PagedResult<WarehouseDto>> ListWarehouses([FromQuery] PagedQuery query, CancellationToken ct)
      => PagedList(query, WarehouseDto.Projection, WarehousesRead, ct);
  // with custom fields:
  => PagedList<Item, ItemDto>(query, "item", ItemDto.From, CorePermissions.ItemsRead, ct);
  ```
- Frontend: `CrudApiService<TDto, TInput>` (one line per resource), `createPagedList(source)` state, and the `<erp-paged-table>` component (search box, server sort, paging, empty/loading states).

## API conventions
- Versioned under `/api/v1`; module APIs under `/api/v1/m/{moduleKey}/...`. A disabled module returns 403 with type `.../module-disabled`; its data is kept.
- **Response envelope** on every endpoint (applied by `ApiEnvelopeResultFilter` to every controller; actions return plain values / `Created(...)` / `NoContent()`):
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
- **Live permission changes**: role edits, role assignments, deactivation, module/capability/industry switches call `IAccessChangeNotifier` after saving. It pushes `access-changed` over the SignalR hub (`/hubs/notifications`) to the affected members and, when access was narrowed, ends their access tokens in that workspace (refresh tokens stay valid, so the next call refreshes silently with the new permissions). The web app (`core/realtime`) and the phone app (`lib/core/access_sync.dart`) then refresh the session, so menus, buttons and route guards follow at once.
- **Coverage tests**: `PermissionCoverageTests` fail when an endpoint has no explicit access rule, asks for a permission missing from the catalogue, or lets a member without permissions through (a runtime sweep of every permission-protected route); menu items must carry catalogue permissions too.

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
