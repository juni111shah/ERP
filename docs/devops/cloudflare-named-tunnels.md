# Permanent URLs: Cloudflare named tunnels (plan, not yet applied)

Today each environment has a Cloudflare **quick tunnel** (`*.trycloudflare.com`). It's free and needs no account, but
the URL changes whenever the tunnel restarts or the PC reboots, and there is no uptime guarantee. This page is the plan
for moving to permanent hostnames once there is a domain on a Cloudflare account. **Nothing here has been done yet.**
The steps that need the Cloudflare login or admin rights are yours.

Placeholders: `<domain>` is your domain (its DNS must be on Cloudflare). `<TUNNEL-ID>` is the UUID that
`tunnel create` prints. `<user>` is your Windows user name.

| Environment | Hostname (placeholder) | Origin on the PC | Notes |
|---|---|---|---|
| dev | `erp-dev.<domain>` | `http://localhost:4200` (`ng serve`, which proxies `/api` and `/hubs` to :5080) | needs the host-header override, see below |
| staging | `erp-staging.<domain>` | `http://localhost:5180` (API + SPA, one origin) | |
| production | `erp.<domain>` | `http://localhost:5280` (API + SPA, one origin) | |

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

Check it before starting: `& $cf tunnel ingress validate` and
`& $cf tunnel ingress rule https://erp-staging.<domain>/` (shows which rule matches).

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
& $cf tunnel run erp          # foreground test; Ctrl+C to stop. Open the three hostnames.
```

As a Windows service (**admin PowerShell, your step**):

```powershell
# The service runs as LocalSystem and reads its config from the system profile, not from your user profile:
$sys = 'C:\Windows\System32\config\systemprofile\.cloudflared'
New-Item -ItemType Directory -Force $sys | Out-Null
Copy-Item "$env:USERPROFILE\.cloudflared\config.yml", "$env:USERPROFILE\.cloudflared\<TUNNEL-ID>.json" $sys
# edit $sys\config.yml so credentials-file points at $sys\<TUNNEL-ID>.json
& $cf service install
Get-Service cloudflared; Start-Service cloudflared
```

Alternatively, manage the tunnel from the Cloudflare dashboard (Zero Trust → Networks → Tunnels). There you set the
public hostnames and the dev host header in the UI and install with `cloudflared service install <token>`. Then no
config or credentials files sit on disk, but the token is a secret: don't paste it anywhere else. Only one service
named `cloudflared` can exist, so per-environment tunnels as services need the dashboard approach or `sc.exe create`
with distinct names.

## 4. App config changes

The app runs behind cloudflared on loopback. `UseForwardedHeaders` already trusts loopback proxies, so the API sees
`https` and the real client IP. HSTS is already on outside Development.

| Setting | staging (`appsettings.Staging.json`) | production (`appsettings.Production.json`) | dev |
|---|---|---|---|
| `Email:AppBaseUrl` (links in emails) | `https://erp-staging.<domain>` | `https://erp.<domain>` | `https://erp-dev.<domain>` if dev mails should link to the tunnel |
| `Cors:Origins` | add `https://erp-staging.<domain>` (keep `http://localhost:5180`) | add `https://erp.<domain>` (keep `http://localhost:5280`) | add `https://erp-dev.<domain>` |
| `Auth:SecureCookies` | already `true`, so no change | already `true`, so no change | `false` today; can stay, or set `true` if dev is only used through HTTPS |

Notes:

- Staging and production serve the SPA and the API from one origin, so CORS isn't involved in normal use. List the
  hostname anyway so tools and other clients behave the same.
- The refresh cookie is `HttpOnly`, `SameSite=Strict` and path-scoped. Same-origin over HTTPS works unchanged. Moving
  from a `trycloudflare.com` URL to the new hostname means everyone logs in once more, because cookies don't carry
  across hosts.
- Staging and production changes go to `main` through a PR, then the normal promotion, so the next deploy picks them
  up. For dev, set them locally (VS launch profile environment variables such as `Email__AppBaseUrl` or
  `Cors__Origins__1`) instead of committing workstation-specific values.
- None of these values are secrets. The signing key stays in Windows Credential Manager.

## 5. Retire the quick tunnels

1. Make sure the named tunnel serves all three hostnames (GET `/` returns the app, and
   `POST /api/v1/auth/login` with a fake user returns 401).
2. In `deploy\windows\environments.psd1` (via PR), set `PublicUrl = 'https://erp-staging.<domain>'` for staging and
   `PublicUrl = 'https://erp.<domain>'` for production. With `PublicUrl` set, `deploy.ps1` and `erp-env.ps1` no longer
   start a quick tunnel for that environment. They stop the one they started (found through `tunnel.pid`), and they
   report the permanent URL. They never touch the named-tunnel service.
3. Until that change is deployed, or to stop them right away:
   `D:\ERP\envs\tools\erp-env.ps1 tunnel-stop -Environment all`. Without step 2, the next deploy or
   `erp-env.ps1 start` would start them again.
4. Dev quick tunnel: it was started by hand. Stop that `cloudflared.exe` process (the one with
   `--url http://localhost:4200`) after telling whoever is testing through it, and give them `https://erp-dev.<domain>`.
5. Optionally put **Cloudflare Access** (Zero Trust → Access → Applications) in front of `erp-dev` and `erp-staging`
   so only invited emails reach them.
6. Update the URLs in `README.md` and `operations.md`.

Rollback: start the quick tunnels again with `erp-env.ps1 tunnel-restart -Environment <env>` (after removing
`PublicUrl`) and stop the `cloudflared` service.
