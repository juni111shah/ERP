# Release Checklist

Release: `vX.Y.Z` · Target env: Staging → Production · Date (PKT): ____ · Release owner: ____ · SQA sign-off: ____ · Approver (Muhammad): ____

## 1. Scope and readiness
- [ ] Release notes drafted (features, fixes, known issues, migrations, config changes).
- [ ] All stories in scope meet the Definition of Done (`test-strategy.md` §12).
- [ ] No open S1/S2 bugs; S3 bugs reviewed and accepted in writing.
- [ ] Feature flags and module defaults for new orgs reviewed.

## 2. Quality gates (on the release commit)
- [ ] All PR CI gates green (`test-strategy.md` §7), including **Isolation** and **Authorization** traits (never quarantined).
- [ ] **Industry-agnostic gate**: core suite green with zero modules; NetArchTest/boundary lint green; vocabulary lint green; core OpenAPI identical across module states; no core migration from module changes.
- [ ] Nightly suite green on the 2 most recent runs: Playwright (Chromium, Firefox, WebKit, mobile), report totals vs the seed, axe (0 serious/critical).
- [ ] Permission matrix (`permission-matrix-cases.csv`) 100% pass.
- [ ] Coverage thresholds met; flaky quarantine count < 10 and none in core security suites.
- [ ] Performance (from Sprint 8): k6 lists < 500 ms p95, reports < 2 s (§13.2), including the custom-field filter scenario.

## 3. Security
- [ ] ZAP baseline on staging: no High; Mediums triaged.
- [ ] `dotnet list package --vulnerable` and `npm audit --audit-level=high`: none; gitleaks: no secrets.
- [ ] Auth checks on staging: refresh cookie `HttpOnly; Secure; SameSite=Strict`; 429 on brute force; lockout 5 / 15 min with a generic message and retry time; `alg:none` rejected; logout revokes refresh **and** the old access token is rejected immediately on every instance; reset tokens single-use and 30 min; workspace picker shows only the user's workspaces.
- [ ] Security headers present (CSP, HSTS, nosniff, Referrer-Policy, frame-ancestors).
- [ ] Secrets are in Key Vault or environment variables, none in images or config (§7.3); JWT signing key rotation plan confirmed.
- [ ] Before v1.0: external pen test findings fixed or accepted (§13 sprint 14).

## 4. Data and migrations
- [ ] EF migration bundle tested on a copy of production (or the last release schema): forward OK, duration recorded, rollback or forward-fix documented.
- [ ] Module migrations are isolated from core history (CORE-024).
- [ ] Backup taken and a **restore verified** right before deploy (§13.3).
- [ ] Seeders: QA seed disabled in Production (guard verified).
- [ ] Tenant isolation smoke on staging with Org A + Org B (TI-001, TI-002, TI-012, TI-016, CORE-011).

## 5. Deploy
- [ ] Image tag = release commit; same image promoted from staging.
- [ ] Manual approval recorded for production (§13.3).
- [ ] Blue-green / slot swap; health checks green; Hangfire servers healthy, recurring jobs registered once.
- [ ] Config diff reviewed (rate limits, token lifetimes: access 5 min, refresh 30 days persistent (absolute) / session-only (12 h absolute), reset tokens 30 min; denylist store (Redis) healthy (it fails closed), `ASPNETCORE_ENVIRONMENT` is not `Testing` and `/api/v1/test-support/*` returns 404 (TS-001), CORS origins, cache TTLs, upload limit 10 MB).

## 6. Post-deploy verification (production, within 30 min)
- [ ] Login → Overview → hard reload of a deep URL → sign out (synthetic account in an internal org).
- [ ] Entity switcher rescopes; module toggles respected.
- [ ] Error rate and latency in Seq/App Insights normal for 30 min; no spike in 401/403/500.
- [ ] Background jobs ran on schedule (lease status, alerts, notifications outbox).

## 7. Rollback plan
- [ ] Trigger criteria defined (e.g. any S1, error rate > 2% for 5 min).
- [ ] Previous image tag: ____; DB compatibility of the previous version with the new schema confirmed (expand/contract).
- [ ] Owner on call: ____.

## 8. Sign-off
| Role | Name | Date (PKT) | Result |
|---|---|---|---|
| SQA | | | Go / No-go |
| Dev lead | | | |
| Product owner (Muhammad Junaid) | | | |
