---
id: TASK-012
title: Remove hardcoded currency, tax rate and placeholder GUIDs from module pages
priority: P1
status: open
schema: no
depends_on: []
area: frontend
audit_ref: AUD-006, AUD-007
commit:
---

## Problem
55 occurrences of `'AED'`, `AED `, `* 0.05`, `taxRate: 5`, `* 1.05` in `frontend/src/app/pages`; `$` in Manufacturing; sample GUID in `crm/interactions.page.ts:304`; `00000000-...-0001` fallbacks in POS, Distribution and `services-analytics.page.ts:167`.

## Impact
Wrong currency and tax for any non-AED workspace; invalid ids posted to the API.

## Change wanted
- Add a `WorkspaceCurrencyService` (core) exposing the workspace currency and formatting helper; use it in every module page instead of literals.
- Take default tax rate from the item's tax code or workspace default tax code; POS totals use the server response.
- Remove every all-zero and sample GUID; if the user id or legal entity is missing, show an error and block the action.
- CRM interactions must send the real related entity id from the selected record.

## Acceptance criteria
- [ ] `grep -rnE "'AED'|AED |\* 0\.05|\* 1\.05|3fa85f64|00000000-0000-0000-0000-00000000000[1-9]" frontend/src/app` returns nothing outside comments and tests.
- [ ] Switching the workspace currency changes labels in Sales, POS, Retail and Manufacturing pages.

## Tests required
Spec for `WorkspaceCurrencyService`; `ng build` clean.

## Do not touch
Backend contracts.

## What I did (Antigravity)

## Review (Claude)
