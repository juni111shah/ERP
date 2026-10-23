# ERP

A modular, multi-tenant ERP for any type of business: an industry-neutral core (organizations, users and roles, parties, items, finance documents, custom fields) with pluggable industry modules and country packs.

- `backend/`: ASP.NET Core 8 Clean Architecture solution
- `frontend/`: Angular 19 + ng-zorro-antd app
- `database/`: SQL scripts
- `deploy/`: Docker / deployment files
- `docs/`: contributing guide and team specs (`docs/team/`)

See [AGENTS.md](AGENTS.md) for coding rules and [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) for the workflow.

> Status: sprint 0 work in progress.


## Run it locally

```bash
./run-dev.sh        # API on :5080 (SQLite, seeded) + web app on :4200
```

Open http://localhost:4200 and sign in with `admin@demo.local` / `DevOnly#2026` (general workspace) or `owner@demo.local` / `DevOnly#2026` (choose workspace `demo-re` for Real Estate). Tenant portal: http://localhost:4200/portal/login. Requires the .NET 8 SDK and Node 20+.
