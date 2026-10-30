# Manual steps for the owner

These steps need your GitHub sign-in, your Windows password, or admin rights, so the automation does not do them.

## 1. Register the self-hosted runner (required for automatic staging/production deploys)

The runner is already downloaded and extracted in `C:\actions-runner`: official `actions/runner` v2.338.1, win-x64,
SHA-256 checked.

1. On GitHub, open **Settings → Actions → Runners → New self-hosted runner** (Windows) and copy the registration token.
   The token is valid for 1 hour. Don't paste it anywhere else.
2. In a normal (non-admin) PowerShell, as the same Windows user that owns the deploy keys:

```powershell
cd C:\actions-runner
.\config.cmd --url https://github.com/juni111shah/ERP --token <TOKEN> --name erp-deploy-pc --labels erp-deploy --work _work --unattended
.\run.cmd                      # leave this window open, or:
D:\ERP\envs\tools\register-startup.ps1 -IncludeRunner   # starts runner + environments at logon (no admin)
```

Alternative: run it as a Windows service. This needs an elevated PowerShell. Use your own account so Credential Manager
and SQL Windows authentication keep working:

```powershell
cd C:\actions-runner
.\config.cmd --url https://github.com/juni111shah/ERP --token <TOKEN> --name erp-deploy-pc --labels erp-deploy --work _work --unattended --runasservice --windowslogonaccount "$env:COMPUTERNAME\<you>"
# it prompts for the Windows password; type it yourself
```

**The repository is public.** A pull request from a fork could try to run its own workflow on a self-hosted runner.
Before you register the runner, do one of the following:
- make the repository private, or
- set **Settings → Actions → General → Fork pull request workflows from outside collaborators → Require approval for all external contributors**,
  and never approve a fork run that targets `self-hosted`.

## 2. Protect production (proposed; not enabled)

The `deploy` job uses GitHub environments `staging` and `production`, which GitHub creates automatically on first use
without any protection. Proposed settings:

- **Environment `production`** (Settings → Environments → production):
  - Required reviewers: you. Prevent self-review if there is a second maintainer.
  - Deployment branches: *Selected branches* → `production` only.
- **Environment `staging`**: Deployment branches → `staging` only. No reviewer needed.
- **Branch rulesets** (Settings → Rules → Rulesets):
  - `main` (in place): PR required, no force push, no deletion. Add required status checks `Backend build and test`
    and `Frontend build` (source GitHub Actions) once they have run under these names.
  - `dev`, `staging`, `production`: block force pushes, restrict deletions, require the same two checks. Restrict who
    can update them to you (and the runner never pushes). If you also require a PR, add yourself to the bypass list.
    Promotions are ff-only pushes, and GitHub's merge button would create a commit that isn't on `staging`, which the
    production gate rejects. See [pipeline.md](pipeline.md#contributor-flow).

The workflow also refuses to deploy to production unless the commit is already on `staging`, so promotion order holds
even before you add these rules.

## 3. First admin in staging and production

There are no seeded users and self-signup is off. Create the first workspace once per environment:

```powershell
cd D:\ERP\envs\tools
.\erp-env.ps1 restart -Environment production -AllowSelfSignup
# open the production URL (.\erp-env.ps1 url) and register the first workspace right away
.\erp-env.ps1 restart -Environment production
```

## 4. Optional: run the APIs as Windows services (admin)

They currently run as background processes started by `deploy.ps1` / `erp-env.ps1`, and restart at logon through
`register-startup.ps1`. The API already supports `UseWindowsService`. Services aren't configured because they need admin
rights and the signing key would have to go in the service's environment. If you want services later, they must run as
your account (for Windows SQL auth), and the deploy scripts would need a service mode. Ask for that as a follow-up.

## 5. Permanent URLs

Quick tunnels (`*.trycloudflare.com`) get a new random URL whenever they restart. For stable URLs you need a Cloudflare
account and a domain on Cloudflare. Then it's one script, run three times
([cloudflare-named-tunnels.md](cloudflare-named-tunnels.md#run-order)):

```powershell
# 0. (you) add <domain> to Cloudflare, point its nameservers there, wait until the zone is Active
# 1. normal PowerShell; opens the browser for `cloudflared tunnel login` (you)
powershell -NoProfile -ExecutionPolicy Bypass -File <checkout>\deploy\cloudflare\setup-named-tunnel.ps1 -Domain <domain>
# 2. ELEVATED PowerShell (you): installs the cloudflared Windows service
powershell -NoProfile -ExecutionPolicy Bypass -File <checkout>\deploy\cloudflare\setup-named-tunnel.ps1 -Domain <domain> -InstallService
# 3. normal PowerShell again: switches staging/production to the permanent URLs and stops their quick tunnels
powershell -NoProfile -ExecutionPolicy Bypass -File <checkout>\deploy\cloudflare\setup-named-tunnel.ps1 -Domain <domain>
```

Add `-WhatIf` to any run to see what it would do. The dev quick tunnel keeps running until you add `-StopDevQuickTunnel`.
Cloudflare Access can put a login in front of dev and staging.
