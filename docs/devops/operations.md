# Operations

All commands run in Windows PowerShell on the deploy PC as the deploy user (the account that owns the Credential Manager
keys and runs the runner). Use the stable copy of the scripts:

```powershell
cd D:\ERP\envs\tools
powershell -NoProfile -ExecutionPolicy Bypass -File .\erp-env.ps1 status
```

(`-ExecutionPolicy Bypass` is only needed if your policy blocks local scripts. The examples below omit the wrapper.)

## Normal deploy (through the pipeline)

1. Merge the feature work into `main` (CI runs).
2. Promote to dev: open a PR `main → dev` and merge it. This builds and tests.
3. Promote to staging: open a PR `dev → staging` and merge it. This deploys staging, so check it at the staging URL.
4. Promote to production: open a PR `staging → production` and merge it. The job waits for approval, if configured, then
   backs up `ERP_Prod` and deploys.

Use merge commits or fast-forwards. Never force-push. Production only accepts commits that are already on staging.

## Manual deploy (no pipeline, same script)

```powershell
cd D:\ERP\deploy\src
git fetch origin; git checkout --detach origin/staging          # or the commit you want
.\deploy\windows\build-package.ps1 -OutDir D:\ERP\deploy\packages\manual
.\deploy\windows\deploy.ps1 -Environment staging -Package D:\ERP\deploy\packages\manual
```

## Rollback

```powershell
.\deploy.ps1 -Environment production -Rollback                       # previous release
.\deploy.ps1 -Environment production -Rollback -To 20261023-170000-abc1234   # a specific folder in releases\
```

Rollback only switches the binaries. It does **not** undo migrations. If the newer release changed the schema in a way the
older one can't use, restore the backup taken before the deploy (production), then roll back:

```powershell
.\erp-env.ps1 stop -Environment production
sqlcmd -S . -E -Q "RESTORE DATABASE [ERP_Prod] FROM DISK=N'ERP_Prod_<timestamp>.bak' WITH REPLACE"
.\deploy.ps1 -Environment production -Rollback
```

You can also roll back through git: revert the bad commit on `staging`, promote again, and production redeploys.

## Status, start, stop

```powershell
.\erp-env.ps1 status                          # ports, PIDs, release, tunnel URL for both environments
.\erp-env.ps1 restart -Environment staging
.\erp-env.ps1 stop    -Environment production # stops the API and its tunnel
.\erp-env.ps1 start   -Environment all        # after a reboot: both APIs and tunnels
```

To start automatically at logon without admin rights: `.\register-startup.ps1 -IncludeRunner` (Startup-folder shortcuts).

## Tunnels and URLs

Quick-tunnel URLs are random and change on every tunnel restart, which includes reboots.
Once the named tunnel is set up ([cloudflare-named-tunnels.md](cloudflare-named-tunnels.md)), an environment listed in
`D:\ERP\envs\public-urls.psd1` has a permanent URL instead: `start`/`restart`/deploys don't start a quick tunnel for
it, and `url`/`status` show the permanent URL. The named tunnel itself is the `cloudflared` Windows service.

```powershell
.\erp-env.ps1 url                                   # current public URLs
.\erp-env.ps1 tunnel-restart -Environment staging   # new tunnel, new URL
```

A deploy that only restarts the API leaves the tunnel and its URL as they are.
Dev tunnel (the workstation UI on :4200) is managed by hand:

```powershell
& 'C:\Program Files (x86)\cloudflared\cloudflared.exe' tunnel --no-autoupdate --url http://localhost:4200 --http-host-header localhost
```

## Platform owner (bootstrap)

Staging and production have no seeded users. Make yourself the platform owner once per environment:

```powershell
.\erp-env.ps1 restart -Environment production -FounderEmail you@company.com    # asks for a password if the account is new
.\erp-env.ps1 restart -Environment production                                  # clears the password from the running process
```

This creates the account, an internal workspace for it and the starter plans. Sign in and open `/platform`. From there you
add clients, or open registration at `/register`, with no more restarts. See [platform-console.md](../platform-console.md).

The older `-AllowSelfSignup` switch still works, and starts the new workspace on the default plan. It is no longer needed.

## Logs

- API: `D:\ERP\envs\<env>\logs\app-<yyyyMMdd>.log`
- Tunnel: `D:\ERP\envs\<env>\logs\tunnel-<timestamp>.log`
- Pipeline: the GitHub Actions run, plus the runner's `C:\actions-runner\_diag`

## Secrets rotation

```powershell
.\init-secrets.ps1 -Environment production -Rotate   # everyone is signed out
.\erp-env.ps1 restart -Environment production
```

## Encryption keys for stored secrets

AI service keys are encrypted with ASP.NET Data Protection. Its key files live in `<Storage:Root>\data-protection-keys` (override with `DataProtection:KeysPath`), so they survive deployments. On Windows they are protected for the machine with DPAPI.

Back this folder up with the database. If you restore the database on another machine, or lose the folder, the console marks each AI service "Paste it again" and the owner enters its key once more. Nothing else stored long-term depends on these files, and sessions in progress may need to sign in again.
