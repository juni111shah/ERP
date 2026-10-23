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

**"SqlException: network-related or instance-specific error"?** The plain `dotnet run` / Visual Studio default targets SQL Server, which you may not have. Use `./run-dev.sh`, or pick the launch profile **"ERP.Api (SQLite - no SQL Server needed)"** (Visual Studio dropdown) / run `dotnet run --launch-profile "ERP.Api (SQLite - no SQL Server needed)"` from `backend/src/ERP.Api`. Then start the web app with `cd frontend && npx ng serve --proxy-config proxy.conf.json`.

**Demo data:** `./run-dev.sh` also fills the `demo-re` workspace with sample data (tenants, vendors, 3 properties with units, leases, deposits, billing, receipts, cheques, work orders, preventive maintenance, deals, an acquisition with a mortgage, bills, a budget, HR employees and payroll). Run it manually with `node tools/seed-demo.mjs` (safe to repeat), or skip it with `SEED_DEMO=0 ./run-dev.sh`. If you start the API from Visual Studio instead, run `node tools/seed-demo.mjs` once after it is up.
