# Pipeline

Two workflows:

- `.github/workflows/ci.yml`: build + test on pushes to `main` and agent branches, on PRs into `main`, `dev`,
  `staging` and `production`, and manually. It deploys nothing. Its checks are what PRs into `main` must pass.
- `.github/workflows/cicd.yml`: runs on pushes to `dev`, `staging` and `production` (and manually through *Run workflow*).

## Check names (for required status checks)

The job `name:` values are fixed so rules can reference them. Don't rename them without updating the rules.

| Workflow file | Workflow / job as shown on a PR | Context string for a required status check |
|---|---|---|
| `ci.yml` | `CI / Backend build and test (pull_request)` | `Backend build and test` |
| `ci.yml` | `CI / Frontend build (pull_request)` | `Frontend build` |
| `cicd.yml` | `CI/CD / Build, test, package (push)` | `Build, test, package` (push-only; not useful as a PR check) |

In a ruleset or branch protection rule, add the two `ci.yml` contexts with source *GitHub Actions*. The checks only
appear in the picker after they have run at least once with these names.

## Contributor flow

```
feature branch --PR--> main --ff--> dev --ff--> staging (auto-deploy) --ff--> production (gated)
```

1. **Work on a feature branch** off `main` and open a PR into `main`. The CI checks must pass before merging. `main` is
   protected (PR required, no force push, no deletion), so nobody pushes to it directly.
   ```bash
   git fetch origin
   git switch -c feature/my-change origin/main
   # ...commit...
   git push -u origin feature/my-change
   gh pr create --base main --head feature/my-change --title "..." --body "..."
   gh pr checks --watch            # wait for "Backend build and test" and "Frontend build"
   gh pr merge --squash --delete-branch
   ```
2. **Promote main to dev.** This is a fast-forward of the exact `main` commit. It triggers CI/CD build + test + package
   on `dev`, with no deploy.
3. **Promote dev to staging.** Fast-forward. CI/CD builds and deploys to staging (:5180). Check it through the staging
   URL.
4. **Promote staging to production.** Fast-forward. CI/CD builds, then the gate checks that the commit is already on
   `staging`. If the `production` environment has a required reviewer, the job waits for approval. Then it backs up
   `ERP_Prod` and deploys (:5280).

Promotions are always fast-forwards of a commit that already exists upstream. They never create a merge commit, and
they're never forced. Steps 2-4 use the same commands with different branch names. Run them in a clean clone (not
the dev working copy):

```bash
git fetch origin
# dev <- main   (staging <- dev: use origin/dev and staging; production <- staging: use origin/staging and production)
git merge-base --is-ancestor origin/dev origin/main && echo "ff ok" || echo "DIVERGED - stop"
git push origin origin/main:refs/heads/dev        # plain push: GitHub rejects it if it isn't a fast-forward
gh run list --branch dev --limit 3                # see the CI/CD run it started
gh run watch                                      # pick the run to follow it
```

```bash
git push origin origin/dev:refs/heads/staging         # dev -> staging
git push origin origin/staging:refs/heads/production  # staging -> production
```

Never add `--force` or `--force-with-lease`. If the plain push is rejected, the target has commits that the source
doesn't have. Find out why (usually a hotfix made straight on the target) and get it into `main` through a PR first.

**Where the env branches are protected:** GitHub's merge button never fast-forwards. *Create a merge commit*,
*Squash* and *Rebase* all create new commits. A merge commit on `production` is not on `staging`, so the production
gate would reject it. So:

- Use the PR for **review only**: `gh pr create --base staging --head dev --title "Promote dev to staging"`. Wait for
  the CI checks and the approval, then **close it and do the fast-forward push above**. Don't click merge.
- In the env branch ruleset, put yourself (or a "release" role) in the **bypass list** for the PR rule. Keep *block
  force pushes* and *restrict deletions* with no bypass. A ff-only push still can't rewrite history.



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
