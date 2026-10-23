# ERP

A modular, multi-tenant ERP for any type of business: an industry-neutral core (organizations, users and roles, parties, items, finance documents, custom fields) with pluggable industry modules and country packs.

- `backend/`: ASP.NET Core 8 Clean Architecture solution
- `frontend/`: Angular 19 + ng-zorro-antd app
- `database/`: SQL scripts
- `deploy/`: Docker / deployment files; Windows deploy scripts (`deploy/windows`) and the Cloudflare named-tunnel setup (`deploy/cloudflare`, run order in [docs/devops/cloudflare-named-tunnels.md](docs/devops/cloudflare-named-tunnels.md#run-order))
- `docs/`: contributing guide and team specs (`docs/team/`)

See [AGENTS.md](AGENTS.md) for coding rules and [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) for the workflow.

> Status: sprint 0 work in progress.


## Run it locally

```bash
./run-dev.sh        # API on :5080 (SQL Server, seeded) + web app on :4200
```

Open http://localhost:4200 and sign in with `admin@demo.local` / `DevOnly#2026` (general workspace) or `owner@demo.local` / `DevOnly#2026` (choose workspace `demo-re` for Real Estate). Tenant portal: http://localhost:4200/portal/login. Requires the .NET 8 SDK and Node 20+.

**Database: SQL Server only.** In Development the API connects to `Server=localhost;Database=ERP_Dev` with Windows authentication (`appsettings.Development.json`), creates `ERP_Dev` if needed, applies the EF Core migrations and runs the dev seed at startup. In Visual Studio run the `ERP.Api` launch profile (or `dotnet run --launch-profile ERP.Api` from `backend/src/ERP.Api`), then start the web app with `cd frontend && npx ng serve --proxy-config proxy.conf.json`. Without Windows authentication (macOS/Linux, or the SQL Server container in `deploy/docker-compose.yml`) set your own connection string with `dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=ERP_Dev;User Id=sa;Password=<yours>;TrustServerCertificate=True"` or the `ConnectionStrings__Default` environment variable. A `SqlException: network-related or instance-specific error` means no SQL Server is reachable at that address.

**Demo data:** `./run-dev.sh` also fills the `demo-re` workspace with sample data (tenants, vendors, 3 properties with units, leases, deposits, billing, receipts, cheques, work orders, preventive maintenance, deals, an acquisition with a mortgage, bills, a budget, HR employees and payroll). Run it manually with `node tools/seed-demo.mjs` (safe to repeat), or skip it with `SEED_DEMO=0 ./run-dev.sh`. If you start the API from Visual Studio instead, run `node tools/seed-demo.mjs` once after it is up.

## Email

Reminders to tenants and vendors, sign-in codes for the portals and password-reset links are sent through SMTP when the `Email` section is enabled. It is off by default (messages are only written to the Message log).

```bash
cd backend/src/ERP.Api
dotnet user-secrets set "Email:Enabled" "true"
dotnet user-secrets set "Email:Host" "smtp.yourprovider.com"
dotnet user-secrets set "Email:Port" "587"
dotnet user-secrets set "Email:UseSsl" "true"
dotnet user-secrets set "Email:Username" "your-user"
dotnet user-secrets set "Email:Password" "your-password"
dotnet user-secrets set "Email:FromAddress" "no-reply@yourcompany.com"
dotnet user-secrets set "Email:AppBaseUrl" "https://erp.yourcompany.com"   # used in password-reset links
```

In production use environment variables (`Email__Host`, `Email__Password` ...). Never commit real values. Open **Administration > Message log** and press **Send test email** to check the settings. A message that fails to send is kept as *Failed* with the error and retried by the next reminder run, up to 3 attempts.
