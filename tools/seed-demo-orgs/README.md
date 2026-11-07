# Demo organizations

Twelve clearly labelled DEMO workspaces, one per industry, built through the real API so that every permission, validation rule and ledger posting is the product's own. All people, companies and contact details are invented.

## Run

Start an API with a seeding flag on its own port. The flag only works in Development, and the two others stop start-up from writing anything else.

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5390 Seed__AllowDemoClock=true Seed__Enabled=false RateLimiting__LoginPerMinute=100000 dotnet run --project backend/src/ERP.Api --no-launch-profile
```

Then seed and verify. Set `ConnectionStrings__Default` for the API and `SQL_DATABASE` for verify if you are not using the default database.

```bash
node tools/seed-demo-orgs/seed.mjs
```

```bash
node tools/seed-demo-orgs/verify.mjs
```

Pass org keys to work on some of them: `node tools/seed-demo-orgs/seed.mjs retail logistics`. Keys are in `orgs.mjs`.

## Safe to run again

Each finished step is recorded in the workspace (`core.demoSeedCheckpoints`), so a re-run skips it and a failed run continues where it stopped. Only workspaces named in `orgs.mjs` are touched, and existing workspaces are never written to. A step that failed part-way can repeat the documents it had already made in that step.

## How dates work

Documents get real past dates because the seeder sends an `X-Demo-Clock` header that pins the server's business clock. The API honours it only when `ASPNETCORE_ENVIRONMENT=Development` and `Seed__AllowDemoClock=true`. Otherwise the middleware is not mapped at all.

## Signing in

Every person signs in as `<person>@<code>.example.test`, where `<person>` is one of `owner`, `admin`, `finance`, `accountant`, `sales`, `purchase`, `inventory`, `hr`, `ops`, and `<code>` is the workspace code. The password is `DemoOnly#2026`, or `DEMO_PASSWORD` if you set it when seeding. Never use these accounts or the flag on a shared or production host.
