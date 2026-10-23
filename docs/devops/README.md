# DevOps: environments, branches and CI/CD

Everything runs on one Windows PC (the "deploy PC"). Public access goes through Cloudflare **quick tunnels**
(`*.trycloudflare.com`). Quick tunnels are free and need no account, but their URL is random and changes every time the
tunnel restarts. Permanent URLs need a named tunnel (see [manual-steps.md](manual-steps.md#permanent-urls)).

| Environment | Branch       | Runs as                                   | Local URL               | Database      | ASPNETCORE_ENVIRONMENT | Public URL |
|-------------|--------------|-------------------------------------------|-------------------------|---------------|------------------------|------------|
| dev         | `dev`        | VS 2022 (API) + `ng serve` (UI)           | http://localhost:4200 (UI), :5080 (API) | `ERP_Dev`     | Development | quick tunnel → :4200 (started by hand) |
| staging     | `staging`    | `ERP.Api.exe` serving API + Angular build | http://localhost:5180   | `ERP_Staging` | Staging                | quick tunnel → :5180, `erp-env.ps1 url` |
| production  | `production` | `ERP.Api.exe` serving API + Angular build | http://localhost:5280   | `ERP_Prod`    | Production             | quick tunnel → :5280, `erp-env.ps1 url` |

`main` stays the integration branch: feature work merges into `main`, and `main` is promoted `main → dev → staging → production`.

Docs:
- [pipeline.md](pipeline.md): what GitHub Actions does on each branch
- [operations.md](operations.md): deploying, promotion, rollback, tunnels and URLs, status, first admin, logs
- [cloudflare-named-tunnels.md](cloudflare-named-tunnels.md): plan for permanent hostnames (named tunnel), not applied yet
- [manual-steps.md](manual-steps.md): one-time steps the owner does (runner registration, GitHub environment protection, optional services, permanent URLs)

## Layout on the deploy PC

```
D:\ERP\ERP                     developer working copy (dev environment; never touched by the pipeline)
D:\ERP\deploy\src              separate clone used for manual package builds
D:\ERP\deploy\packages\...     manually built packages
D:\ERP\envs\tools              stable copy of deploy/windows/*.ps1 (refreshed on every deploy)
D:\ERP\envs\<env>\releases\    one folder per release: <yyyyMMdd-HHmmss>-<sha7> (last 5 kept)
D:\ERP\envs\<env>\current      junction -> the live release
D:\ERP\envs\<env>\storage      uploaded files (Storage__Root), survives releases
D:\ERP\envs\<env>\logs         app-<date>.log, tunnel-<timestamp>.log
D:\ERP\envs\<env>\tunnel-url.txt, tunnel.pid
C:\actions-runner              GitHub Actions self-hosted runner (labels: self-hosted, Windows, erp-deploy)
```

## Configuration and secrets

- `appsettings.Staging.json` / `appsettings.Production.json` (committed, no secrets): Windows-auth connection string
  (`Server=.;Database=ERP_Staging|ERP_Prod;Trusted_Connection=True;TrustServerCertificate=True`), `Database:AutoMigrate=true`,
  `Seed:Enabled=false`, `Onboarding:AllowSelfSignup=false`, `Swagger:Enabled=false`, secure cookies, CORS = own origin.
- `appsettings.Development.json` is excluded from publish output, so deploy packages never carry dev settings.
- JWT signing key: 64 random bytes per environment, stored in Windows Credential Manager of the deploy user as
  `ERP:staging:SigningKey` / `ERP:production:SigningKey` (created by `init-secrets.ps1`, never printed or written to disk).
  `deploy.ps1` passes it to the API process as `Auth__SigningKey`. No SQL passwords anywhere (Windows authentication).
- Each API process gets `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS=http://localhost:<port>`, `Storage__Root`, `Auth__SigningKey`.

## Database migrations and seeding

- Staging/production start with `Database:AutoMigrate=true`: EF Core `MigrateAsync` creates the database if missing and applies
  every pending migration on startup. A deploy therefore carries all new migrations with it.
- Production deploys take a `COPY_ONLY` backup first (`ERP_Prod_<timestamp>.bak` in the SQL Server default backup folder).
- `Seed:Enabled=false`: no demo users, workspaces or data in staging/production. `Onboarding:AllowSelfSignup=false`: nobody can
  register a workspace from the internet. The first workspace/admin is created once with a temporary switch
  (see [operations.md](operations.md#first-admin-bootstrap)).

## App changes that support this

- `UseWindowsService` support (optional service hosting), content root = install folder when running as a service.
- Forwarded headers from loopback (cloudflared) so client IPs and https scheme are correct.
- When the publish folder contains `wwwroot/index.html`, the API serves the Angular app on the same origin and falls back to
  `index.html` for client routes; `/api`, `/hubs`, `/swagger` and `/health` are never rewritten. With no `wwwroot` (dev),
  behaviour is unchanged: `/` redirects to Swagger.
