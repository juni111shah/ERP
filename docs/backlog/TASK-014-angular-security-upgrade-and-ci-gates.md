---
id: TASK-014
title: Upgrade Angular to patched 19.x and add security gates to CI
priority: P0
status: done
schema: no
depends_on: []
area: devops
audit_ref: AUD-050, AUD-041
commit: 5709c29
---

## Problem
`npm audit --omit=dev` reports 10 vulnerabilities (5 high) in `@angular/*` 19.2. CI runs no audit, lint, frontend tests or backend vulnerability check.

## Change wanted
- Upgrade `@angular/*`, `@angular/cdk`, `@angular/cli`, build packages to the newest patched 19.x release that clears the production advisories; if only a major upgrade clears them, stop and set `status: blocked` with the findings.
- Add to `.github/workflows/ci.yml`: `npm audit --omit=dev --audit-level=high`, `ng test --watch=false --browsers=ChromeHeadless`, `dotnet list backend/ERP.sln package --vulnerable --include-transitive` (fail on High/Critical), and a step that fails if a migration has no matching script in `database/scripts`.
- The `package-lock.json` change is allowed for this card only.

## Acceptance criteria
- [ ] `npm audit --omit=dev` shows no high or critical.
- [ ] `ng build` and `ng test` pass.
- [ ] CI file contains the new steps.

## Tests required
Existing suites.

## Do not touch
Application code beyond what the upgrade requires.

## What I did (Antigravity)
- Executed `npm audit --omit=dev` in `frontend/`. It reported 10 vulnerabilities (5 high, 5 moderate) across `@angular/common`, `@angular/compiler`, and `@angular/core` affecting `<=19.2.25`.
- Verified available 19.x releases via `npm view @angular/core versions`. The latest release in the 19.x train is `19.2.25`, which is already installed in `node_modules` and locked.
- None of the high severity vulnerabilities (GHSA-48r7-hpm6-gfxm, GHSA-39pv-4j6c-2g6v, GHSA-jhpw-976m-542j, GHSA-p297-fm68-3q8c, GHSA-jfgx-762w-99vr, GHSA-58w9-8g37-x9v5, GHSA-jj27-h5hq-8x99, GHSA-hh8m-fm6v-7cvg, GHSA-rgjc-h3x7-9mwg) have a patch in 19.x. The fixes only exist in major releases (v20+ / v22+).
- In accordance with the card instruction ("if only a major upgrade clears them, stop and set status: blocked with the findings"), stopped and marked card as blocked.

## Review (Claude)

## Resolution (Claude, on user request)
A major upgrade was authorised. Angular 19.2 -> 20.3.34 (core, cli, cdk 20.2, ng-zorro-antd 20, icons-angular 20, animations, TypeScript 5.9). `npm audit --omit=dev`: 0 vulnerabilities. Code fixes the upgrade required: template method `void()` renamed `voidDocument()` (bills, invoices, payments, receipts; `void` is reserved in v20 templates), `afterRender` -> `afterEveryRender` (paged-table), RTL switch uses `Directionality.valueSignal` (CDK 20 made `value` a getter). `ng build` 0 errors, `ng test` 8/8 pass. CI: added `npm audit --omit=dev --audit-level=high`, `ng test`, backend vulnerable-package check (High/Critical fails), and a migration-script check (migrations from 20261029000000 on must appear in database/scripts; older ones were scripted in grouped files).
