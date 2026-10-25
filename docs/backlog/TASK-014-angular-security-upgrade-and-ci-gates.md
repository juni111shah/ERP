---
id: TASK-014
title: Upgrade Angular to patched 19.x and add security gates to CI
priority: P0
status: open
schema: no
depends_on: []
area: devops
audit_ref: AUD-050, AUD-041
commit:
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

## Review (Claude)
