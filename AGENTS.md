# AGENTS.md: rules for every AI coding tool (Antigravity, Claude Code, Cursor, Codex)

## Product
A general-purpose, modular ERP for **any** business. The core is industry-neutral and country-neutral.
Industries (real estate, retail, inventory, HR, manufacturing...) plug in as **modules**; country rules (VAT returns, payroll files, tenancy registration...) ship as **country packs**.

**Hard rule:** never put industry- or country-specific concepts in the core (`backend/src`, core SQL, core UI). The core vocabulary denylist test (`backend/tests/ERP.UnitTests/core-vocabulary-denylist.txt`) enforces this.

## Stack
- Backend: ASP.NET Core 8, Clean Architecture: `ERP.Domain` -> `ERP.Application` (MediatR CQRS + FluentValidation) -> `ERP.Infrastructure` (EF Core, SQL Server) -> `ERP.Api`. `ERP.Modularity` holds the `IModule` contract and registry.
- Frontend: Angular 20 + ng-zorro-antd 20, standalone components and signals, theme CSS variables `--erp-*`. Layout and rules: `frontend/ARCHITECTURE.md` (reference: `features/sales`).
- Realtime: SignalR. Logging: Serilog.

## Code rules
1. **Clean architecture**: Domain has no dependencies; Application depends on Domain only; Infrastructure implements Application abstractions. No EF types in Domain.
   **Request flow is Controller → Command/Query handler → Repository → Database** (see `docs/ARCHITECTURE.md`, reference: Invoices). A controller (`ERP.Api/Controllers/<Area>/` or `<Module>/Controllers/`, derived from `ApiControllerBase`) only sends one MediatR request per action. Each request is a `<Verb><Thing>Command`/`<Verb><Thing>Query` in its own folder (`<Area>/<Feature>/Commands/<Name>/`) with exactly one `<Verb><Thing>Handler` and, when it has input, a `<Verb><Thing>Validator`. Handlers use repositories: the generic `IRepository<T>` for plain loads/saves, an `I<Entity>Repository` (implementation derived from `RepositoryBase<T>` in `Repositories/`) for anything more. Nothing outside Infrastructure touches `DbContext`. New list = a controller action calling `PagedList(...)` (no handler). Architecture tests and the route snapshot enforce all of this.
2. **Reuse first**: before writing a helper, search for an existing one. Shared behaviour lives in `ERP.Application/Common` (backend) and `frontend/src/app/core` and `frontend/src/app/shared` (frontend).
3. **Pagination, sorting and search are generic**: use the shared `PagedQuery`/`PagedResult<T>` and `IQueryable` extensions (`ApplyPaging`, `ApplySearch`, `ApplySort`, `ApplyFilters`) with whitelisted fields. Never hand-write paging per table.
4. **Standard API response**: every endpoint returns `ApiResponse<T>` `{ success, statusCode, message, data, errors, traceId }`. Errors are also RFC 7807 compatible.
5. **Multi-tenancy**: every business entity is `OrgEntity` / `IOrgScoped`; the global query filter scopes by OrgId. The UI calls an organization a "Workspace".
6. **Modules**: module APIs live at `/api/v1/m/{moduleKey}/...`; core APIs at `/api/v1/...`. A disabled module returns 403 `module-disabled` and keeps its data.
7. **Permissions**: `<module>.<resource>.<action>`, all lowercase, plural resource, `.read` (never `.view`). Examples: `core.customfields.manage`, `finance.cheques.approve`, `realestate.unittypes.read`.
8. **Custom fields**: one `CustomFields` JSON column per extensible table (ISJSON check), keys `^cf_[a-z0-9_]{1,60}$`, definitions in `CustomFieldDefinition`.
9. Soft delete for master data; optimistic concurrency via ETag/If-Match (412).
10. No secrets, PDFs, screenshots or third-party product names in the repo.

## Git workflow
- `main` is protected: only tested code. Each tool works on its own branch: `antigravity/*`, `claude/*`, `cursor/*`, `codex/*`, merged through pull requests.
- Before a PR: `dotnet build` (0 errors), `dotnet test` (all pass), `ng build` succeeds.
- Conventional commits: `feat(core): ...`, `fix(realestate): ...`.

## Docs
Specs live in `docs/team/` (BA, DB, full-stack, SQA, UI/UX). Read the relevant spec before building a screen or table.

## Automated work queue
Work is driven from `docs/backlog/` on the single branch `antigravity/dashboard-and-modules` (no pushes, no merges to `main`). Read `docs/backlog/README.md` before coding. Antigravity implements one `TASK-NNN` card per commit; Claude reviews cards and writes new ones. Do not work outside a card.
