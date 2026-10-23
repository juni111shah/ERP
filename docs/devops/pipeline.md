# Pipeline

Two workflows:

- `.github/workflows/ci.yml` (existing): build + test on pushes to `main` and agent branches, and on PRs into
  `main`, `dev`, `staging` and `production`. It deploys nothing.
- `.github/workflows/cicd.yml`: runs on pushes to `dev`, `staging` and `production` (and manually through *Run workflow*).

## cicd.yml

| Job | Runs on | What it does |
|-----|---------|--------------|
| `build` | GitHub-hosted `ubuntu-latest` | `dotnet build` + `dotnet test` (unit and integration; integration tests use in-memory SQLite), `dotnet publish -r win-x64` (framework-dependent), `npm ci` + `ng build --configuration production`, copies the Angular `browser` output into `app/wwwroot`, writes `release.json` (sha, branch, run), uploads the artifact `erp-package` (14 days) |
| `deploy` | self-hosted runner `[self-hosted, Windows, erp-deploy]` on the deploy PC | only for `staging` and `production`. Uses GitHub environment `staging` / `production`. Production first checks that the commit is already on `origin/staging` (`git merge-base --is-ancestor`). Then it runs `deploy/windows/deploy.ps1 -Environment <branch> -Package <artifact>` |

Branch behaviour:

- **dev**: build, test, package. The dev environment is the developer workstation (VS + `ng serve`), which is never
  redeployed automatically, so testing that runs there is not interrupted. The artifact is there for inspection.
- **staging**: build, test, package, deploy to :5180 / `ERP_Staging`.
- **production**: build, test, package, then the gate. The commit must come from staging, and the `production` environment
  approval applies once it is configured (see manual-steps.md). After that: back up `ERP_Prod`, then deploy to :5280.

`concurrency` makes deploys to the same environment queue instead of overlapping.

## What deploy.ps1 does

1. Copies `app\` from the package into `D:\ERP\envs\<env>\releases\<timestamp>-<sha7>`.
2. Production only: takes a `COPY_ONLY` database backup.
3. Stops the API listening on the environment port. It refuses to stop anything that isn't `ERP.Api.exe`/`dotnet.exe`.
4. Points the `current` junction at the new release and starts `ERP.Api.exe` detached, with the environment variables
   and the signing key from Credential Manager. Pending EF migrations are applied on startup.
5. Waits up to 180 s for `/health` = 200 and for `GET /` to return the Angular shell. If either fails it prints the log
   tail, switches back to the previous release, and the job fails.
6. Keeps the last 5 releases, refreshes `D:\ERP\envs\tools`, and makes sure the quick tunnel is running. The public URL is
   printed and added to the job summary.

Processes are started through WMI (`Win32_Process.Create`) without the runner's tracking variables, so they keep running
after the job ends.
