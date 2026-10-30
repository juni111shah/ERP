# Permanent URLs: Cloudflare named tunnel

Today each environment has a Cloudflare **quick tunnel** (`*.trycloudflare.com`). It's free and needs no account, but
the URL changes whenever the tunnel restarts or the PC reboots, and there is no uptime guarantee. This page moves the
deploy PC to permanent hostnames on your own domain with one **named tunnel**. Everything is scripted in
[`deploy/cloudflare/setup-named-tunnel.ps1`](../../deploy/cloudflare/setup-named-tunnel.ps1); the steps that need your
Cloudflare login or admin rights are marked **(you)**.

Placeholders: `<domain>` is your domain (its DNS must be on Cloudflare). `<TUNNEL-ID>` is the UUID that
`tunnel create` prints. `<user>` is your Windows user name.

| Environment | Hostname (default) | Origin on the PC | Notes |
|---|---|---|---|
| dev | `erp-dev.<domain>` | `http://localhost:4200` (`ng serve`, which proxies `/api` and `/hubs` to :5080) | host header rewritten to `localhost` |
| dev API (optional) | `erp-dev-api.<domain>` | `http://localhost:5080` (VS API) | off by default, `-DevApiHost erp-dev-api` |
| staging | `erp-staging.<domain>` | `http://localhost:5180` (API + SPA, one origin) | |
| production | `erp.<domain>` | `http://localhost:5280` (API + SPA, one origin) | |
| anything else | | `http_status:404` | |

Template: [`deploy/cloudflare/config.example.yml`](../../deploy/cloudflare/config.example.yml). The real `config.yml`
and the credentials are generated outside the repo.

## Run order

0. **(you)** Have the domain on Cloudflare: add the site in the Cloudflare dashboard (free plan is enough) and change
   the nameservers at your registrar to the two Cloudflare shows. Wait until the zone is **Active**.
1. Normal PowerShell, from an up-to-date checkout (or `D:\ERP\envs\tools\cloudflare` after the next deploy):

   ```powershell
   cd <checkout>\deploy\cloudflare
   powershell -NoProfile -ExecutionPolicy Bypass -File .\setup-named-tunnel.ps1 -Domain <domain> -WhatIf   # dry run
   powershell -NoProfile -ExecutionPolicy Bypass -File .\setup-named-tunnel.ps1 -Domain <domain>
   ```

   **(you)** the browser opens for `cloudflared tunnel login`: sign in and pick `<domain>`. The script then creates
   tunnel `erp` (or reuses it), routes DNS for each hostname, writes `%USERPROFILE%\.cloudflared\config.yml`,
   validates it, and prints the exact `-InstallService` command.
2. **(you)** Elevated PowerShell (Run as administrator), same command plus `-InstallService`. It copies the config
   and the tunnel credentials to `C:\Windows\System32\config\systemprofile\.cloudflared`, installs (or restarts)
   the `cloudflared` service, pins its command line to that config, and starts it. Nothing else.
3. Normal PowerShell, the step-1 command again. For staging and production it checks that
   `https://<host>/health` returns 200 and `/` returns the app, then writes `D:\ERP\envs\public-urls.psd1`,
   restarts that API (so it picks up the URL, see "App config") and stops its quick tunnel. If a hostname doesn't
   answer yet, that environment stays on its quick tunnel; re-run later. Re-running is always safe.
4. Dev: the dev quick tunnel (`--url http://localhost:4200`, used by SQA) is **left running**. Once SQA has
   `https://erp-dev.<domain>`, re-run with `-StopDevQuickTunnel`.
5. Check: `D:\ERP\envs\tools\erp-env.ps1 status` (PublicUrl shows the permanent URLs) and open the hostnames.
   Everyone logs in once more on the new hostnames (cookies don't move between hosts).

Parameters: `-DevHost`, `-StagingHost`, `-ProductionHost`, `-DevApiHost` (a label goes under `-Domain`, a value with
a dot is used as-is), `-TunnelName` (default `erp`), `-Cloudflared <path>`, `-OverwriteDns` (replace an existing DNS
record), `-ForceLogin`, `-SkipSwitch` (stop after the tunnel/service part), `-StopDevQuickTunnel`, `-WhatIf`.

## Dev API hostname: not needed (optional)

The dev SPA calls the API with the relative base URL `/api/v1` (`API_BASE_URL`), and `ng serve` proxies `/api` and
`/hubs` to :5080. Through `erp-dev.<domain>` the browser therefore talks to one origin: no CORS, no absolute API URL,
the `SameSite=Strict` refresh cookie works, and SignalR goes over the same WebSocket proxy. One dev hostname is enough.

A separate `erp-dev-api.<domain>` → :5080 is only useful for Swagger, Postman, scripts or a native client. Pass
`-DevApiHost erp-dev-api` to add it. Swagger UI on that host is same-origin, so it works without changes. If a browser
app on `erp-dev.<domain>` should call it directly (cross-origin), add the web origin to the dev API's CORS by setting
`ERP_Hosting__PublicUrl=https://erp-dev.<domain>` in the VS launch profile on the workstation (not committed); cookies
still work because both hosts are the same site. It also exposes dev Swagger publicly, so leave it off unless needed,
or put Cloudflare Access in front of it.

## App config: `Hosting:PublicUrl`

The permanent URL is set **once per machine**, not in committed appsettings:

- `D:\ERP\envs\public-urls.psd1` (written by the script, step 3), for example
  `@{ staging = 'https://erp-staging.<domain>'; production = 'https://erp.<domain>' }`. `Get-ErpEnv` reads it
  (it wins over a `PublicUrl` in `environments.psd1`).
- `Start-ErpApp` passes it to the API as `ERP_Hosting__PublicUrl` (always set, empty when there is none, so a value in
  your user environment can't leak into staging/production).
- The API (`PublicOrigin`, `Program.cs`) reads `Hosting:PublicUrl` (any config source; env vars with prefix `ERP_`).
  When set, it must be an origin (`https://host[:port]`, no path), and it is **added** to `Cors:Origins` and used as
  `Email:AppBaseUrl` (password-reset links). When unset, nothing changes: localhost and quick tunnels work as before.
- `deploy.ps1` / `erp-env.ps1` with a PublicUrl don't start a quick tunnel; they stop the one they started (found via
  `tunnel.pid` and its `localhost:<port>` command line) and never touch the named-tunnel service.
- No secrets are involved. `Auth:SecureCookies` is already `true` for staging/production; `UseForwardedHeaders`
  trusts loopback (cloudflared), so the API sees `https`.
- No Angular or `angular.json` change: the SPA uses relative URLs, and `httpHostHeader: localhost` satisfies the dev
  server's host check.

## Rollback

Per environment: remove its line from `D:\ERP\envs\public-urls.psd1`, then
`D:\ERP\envs\tools\erp-env.ps1 restart -Environment <env>` (starts a quick tunnel again and drops the URL from
CORS/email). Whole tunnel: `Stop-Service cloudflared` (elevated). Dev: start the quick tunnel by hand as before.

## Manual reference

What the script does, step by step, if you prefer to do it by hand or need to debug it.

## One tunnel or one per environment?

**Recommended: one tunnel (`erp`) with ingress rules.** All three origins are on the same PC, so one tunnel gives you
one credentials file, one Windows service and one config file. A hostname is added with a DNS route and an ingress
line. cloudflared keeps several connections to Cloudflare open, so it's as available as the PC itself.

Separate tunnels per environment only pay off if production moves to another machine, or if you want config changes
and restarts for dev/staging to never touch production. With a locally managed config, editing ingress means
restarting cloudflared, which briefly drops every hostname on that tunnel. If that matters, use two tunnels: `erp`
(production only) and `erp-nonprod` (dev and staging). Both variants are below.

## 1. Create the tunnel and DNS routes (you do this, no admin needed)

```powershell
$cf = 'C:\Program Files (x86)\cloudflared\cloudflared.exe'
& $cf tunnel login                      # opens the browser; pick <domain>. Writes %USERPROFILE%\.cloudflared\cert.pem
& $cf tunnel create erp                 # prints <TUNNEL-ID>, writes %USERPROFILE%\.cloudflared\<TUNNEL-ID>.json
& $cf tunnel route dns erp erp-dev.<domain>
& $cf tunnel route dns erp erp-staging.<domain>
& $cf tunnel route dns erp erp.<domain>
& $cf tunnel list
```

`cert.pem` and `<TUNNEL-ID>.json` are credentials. Keep them out of the repo and out of chat. Don't copy them into
`D:\ERP\deploy` or anything that gets committed.

## 2. config.yml

### Recommended: one tunnel, all environments

`%USERPROFILE%\.cloudflared\config.yml`:

```yaml
tunnel: <TUNNEL-ID>
credentials-file: C:\Users\<user>\.cloudflared\<TUNNEL-ID>.json
no-autoupdate: true

ingress:
  # dev: ng serve only answers to its own host name, so rewrite Host to localhost
  - hostname: erp-dev.<domain>
    service: http://localhost:4200
    originRequest:
      httpHostHeader: localhost
  # staging: API + SPA on one origin
  - hostname: erp-staging.<domain>
    service: http://localhost:5180
  # production: API + SPA on one origin
  - hostname: erp.<domain>
    service: http://localhost:5280
  # anything else
  - service: http_status:404
```

Check it before starting: `& $cf tunnel --config <file> ingress validate` and
`& $cf tunnel --config <file> ingress rule https://erp-staging.<domain>/` (shows which rule matches).

### Alternative: one tunnel per environment

Same `tunnel create` / `route dns` steps per tunnel (`erp-dev`, `erp-staging`, `erp-prod`). One config file each,
started with `cloudflared --config <file> tunnel run`:

```yaml
# config-dev.yml
tunnel: <DEV-TUNNEL-ID>
credentials-file: C:\Users\<user>\.cloudflared\<DEV-TUNNEL-ID>.json
ingress:
  - hostname: erp-dev.<domain>
    service: http://localhost:4200
    originRequest: { httpHostHeader: localhost }
  - service: http_status:404
```

```yaml
# config-staging.yml
tunnel: <STAGING-TUNNEL-ID>
credentials-file: C:\Users\<user>\.cloudflared\<STAGING-TUNNEL-ID>.json
ingress:
  - hostname: erp-staging.<domain>
    service: http://localhost:5180
  - service: http_status:404
```

```yaml
# config-prod.yml
tunnel: <PROD-TUNNEL-ID>
credentials-file: C:\Users\<user>\.cloudflared\<PROD-TUNNEL-ID>.json
ingress:
  - hostname: erp.<domain>
    service: http://localhost:5280
  - service: http_status:404
```

**About the dev host header:** the Angular dev server checks the `Host` header and rejects hostnames it doesn't know.
`httpHostHeader: localhost` makes requests look local, the same as the current dev quick tunnel's
`--http-host-header localhost`. The alternative is `allowedHosts` in `angular.json`, but that file has local edits
on the workstation, so leave it alone. The `/api` and `/hubs` proxy in `proxy.conf.json` already uses
`changeOrigin`, and WebSockets (SignalR, live reload) go through the tunnel without extra settings. Staging and
production need no host override.

## 3. Test it in the foreground, then run it as a service

```powershell
& $cf tunnel --config "$env:USERPROFILE\.cloudflared\config.yml" run erp   # foreground test; Ctrl+C to stop
```

As a Windows service (**admin PowerShell, your step**):

```powershell
# The service runs as LocalSystem and reads its config from the system profile, not from your user profile:
$sys = 'C:\Windows\System32\config\systemprofile\.cloudflared'
New-Item -ItemType Directory -Force $sys | Out-Null
Copy-Item "$env:USERPROFILE\.cloudflared\config.yml", "$env:USERPROFILE\.cloudflared\<TUNNEL-ID>.json" $sys
# edit $sys\config.yml so credentials-file points at $sys\<TUNNEL-ID>.json
& $cf service install
# pin the service to that config (what -InstallService does):
Set-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Services\cloudflared ImagePath "`"$cf`" tunnel --config `"$sys\config.yml`" run erp"
Start-Service cloudflared
```

Alternatively, manage the tunnel from the Cloudflare dashboard (Zero Trust → Networks → Tunnels). There you set the
public hostnames and the dev host header in the UI and install with `cloudflared service install <token>`. Then no
config or credentials files sit on disk, but the token is a secret: don't paste it anywhere else. Only one service
named `cloudflared` can exist, so per-environment tunnels as services need the dashboard approach or `sc.exe create`
with distinct names.

## 4. App config and retiring the quick tunnels

See "App config: `Hosting:PublicUrl`" above. By hand: create `D:\ERP\envs\public-urls.psd1`, then
`erp-env.ps1 restart -Environment <env>` per environment (restarts the API with the URL and stops its quick tunnel).
Optionally put **Cloudflare Access** (Zero Trust → Access → Applications) in front of `erp-dev` and `erp-staging` so
only invited emails reach them.
